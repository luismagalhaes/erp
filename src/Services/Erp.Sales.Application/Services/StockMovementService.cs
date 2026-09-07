using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.QrCode;
using Erp.FiscalPT.Signing;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Sales.Application.Configuration;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Erp.Sales.Application.Services;

/// <summary>
/// Issues the documents covered by the goods in circulation regime. They follow the same rules
/// as an invoice — series numbering, hash chain, immutability — and add the transport data the
/// tax authority requires before the goods start moving.
/// </summary>
public sealed class StockMovementService(
    IStockMovementStorage movementStorage,
    ISeriesStorage seriesStorage,
    ISalesUnitOfWork unitOfWork,
    IDocumentSigner signer,
    IStockRecorder stockRecorder,
    IOptions<FiscalOptions> fiscalOptions) : IStockMovementService
{
    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<IReadOnlyList<StockMovementListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var movements = await movementStorage.GetAllAsync(companyId, cancellationToken);

        return movements
            .Select(x => new StockMovementListItemDto(
                x.Id,
                x.DocumentNumber,
                x.MovementType,
                x.Atcud,
                x.MovementDate,
                x.PartyName,
                x.PartyTaxId,
                x.MovementStartAtUtc,
                x.TotalQuantity,
                x.GrossTotal,
                x.EffectiveStatus,
                x.AtDocCodeId))
            .ToList();
    }

    public async Task<StockMovementDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var movement = await movementStorage.GetByIdAsync(id, cancellationToken);
        return movement is null ? null : Map(movement);
    }

    public async Task<StockMovementDetailDto> IssueAsync(
        CreateStockMovementRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("A movement must have at least one line.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ShipFrom?.Address))
            throw new ArgumentException("The loading address is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ShipTo?.Address))
            throw new ArgumentException("The delivery address is required.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The series row is locked for the whole transaction: without it, two concurrent
        // issues could take the same sequence number or chain onto the same previous hash.
        var series = await seriesStorage.GetForUpdateAsync(request.SeriesId, cancellationToken)
            ?? throw new ArgumentException($"Series '{request.SeriesId}' was not found.", nameof(request));

        if (series.CompanyId != request.CompanyId)
            throw new ArgumentException("The series does not belong to the requested company.", nameof(request));

        if (!MovementDocumentTypes.IsSupported(series.DocumentType))
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' is for '{series.DocumentType}' documents, not for goods movements.",
                nameof(request));

        if (!series.CanIssue)
            throw new InvalidOperationException(
                $"Series '{series.SeriesCode}' has no validation code from the tax authority yet, so it cannot issue documents.");

        var lines = BuildLines(request.Lines);

        var netTotal = FiscalRounding.Amount(lines.Sum(x => x.LineAmount));
        var taxPayable = FiscalRounding.Amount(lines.Sum(x => x.TaxAmount));
        var grossTotal = FiscalRounding.Amount(netTotal + taxPayable);

        var sequenceNumber = series.TakeNextSequence();
        var documentNumber = DocumentNumber.Build(series.DocumentType, series.SeriesCode, sequenceNumber);
        var atcud = Atcud.Build(series.ValidationCode!, sequenceNumber);

        // Seconds precision, because that is what goes into the signed string.
        var systemEntryDateUtc = TruncateToSeconds(DateTime.UtcNow);
        var previousHash = await movementStorage.GetLastHashAsync(series.Id, cancellationToken);

        var signature = signer.Sign(request.MovementDate, systemEntryDateUtc, documentNumber, grossTotal, previousHash);

        var movement = StockMovement.Issue(
            request.CompanyId,
            series,
            sequenceNumber,
            documentNumber,
            atcud,
            request.MovementDate,
            systemEntryDateUtc,
            ToParty(request.Party),
            ToLocation(request.ShipFrom),
            ToLocation(request.ShipTo),
            request.MovementStartAtUtc,
            request.MovementEndAtUtc,
            Normalize(request.VehiclePlate),
            Normalize(request.Comments),
            lines,
            netTotal,
            taxPayable,
            grossTotal,
            signature.Hash,
            previousHash,
            signature.HashControl,
            userId);

        movement.AttachQrCodePayload(BuildQrCodePayload(movement, signature.Hash));

        await movementStorage.AddAsync(movement, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Inside the transaction on purpose: the guia and the stock it moves commit together.
        await RecordStockAsync(series, movement, request.WarehouseId, userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(movement);
    }

    public async Task<StockMovementDetailDto?> CommunicateAsync(Guid id, string atDocCodeId, CancellationToken cancellationToken = default)
    {
        var movement = await movementStorage.GetByIdAsync(id, cancellationToken);
        if (movement is null)
            return null;

        movement.Communicate(atDocCodeId.Trim(), DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(movement);
    }

    public async Task<StockMovementDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        var movement = await movementStorage.GetByIdAsync(id, cancellationToken);
        if (movement is null)
            return null;

        var statusChange = movement.Void(reason, userId, DateTime.UtcNow);

        // Transactional, because voiding also puts back the stock the guia moved: the two have to
        // happen together, or the goods end up outside the warehouse with nothing to show it.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await movementStorage.AddStatusChangeAsync(statusChange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await stockRecorder.ReverseDocumentAsync(
            movement.Id, $"Anulação de {movement.DocumentNumber}: {reason}", userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(movement);
    }

    private static List<StockMovementLine> BuildLines(IReadOnlyList<CreateStockMovementLineRequest> requestLines)
    {
        var lines = new List<StockMovementLine>(requestLines.Count);
        var lineNumber = 1;

        foreach (var line in requestLines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} must have a positive quantity.", nameof(requestLines));

            if (line.UnitPrice < 0)
                throw new ArgumentException($"Line {lineNumber} cannot have a negative unit price.", nameof(requestLines));

            if (!TaxCodes.All.Contains(line.TaxCode, StringComparer.Ordinal))
                throw new ArgumentException($"Line {lineNumber} has an unknown tax code '{line.TaxCode}'.", nameof(requestLines));

            if (string.Equals(line.TaxCode, TaxCodes.Exempt, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(line.TaxExemptionReason))
            {
                throw new ArgumentException($"Line {lineNumber} is exempt and requires an exemption reason.", nameof(requestLines));
            }

            var lineAmount = FiscalRounding.Amount(line.Quantity * line.UnitPrice);
            var taxAmount = FiscalRounding.Amount(lineAmount * line.TaxPercentage / 100m);

            lines.Add(new StockMovementLine
            {
                LineNumber = lineNumber,
                ProductCode = line.ProductCode,
                ProductDescription = line.ProductDescription,
                Quantity = line.Quantity,
                UnitOfMeasure = line.UnitOfMeasure,
                UnitPrice = line.UnitPrice,
                LineAmount = lineAmount,
                TaxCountryRegion = line.TaxCountryRegion,
                TaxCode = line.TaxCode,
                TaxPercentage = line.TaxPercentage,
                TaxAmount = taxAmount,
                TaxExemptionCode = line.TaxExemptionCode,
                TaxExemptionReason = line.TaxExemptionReason
            });

            lineNumber++;
        }

        return lines;
    }

    /// <summary>
    /// Records what the guia does to stock, when its series says it does anything. This is the
    /// document that normally moves the goods; the invoice that follows it will find the stock
    /// already gone and leave it alone.
    /// </summary>
    private async Task RecordStockAsync(
        Series series,
        StockMovement movement,
        Guid? warehouseId,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (series.StockEffect == StockEffect.None)
            return;

        if (warehouseId is not { } warehouse || warehouse == Guid.Empty)
        {
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' moves stock, so the document needs a warehouse.",
                nameof(warehouseId));
        }

        var request = new RecordDocumentStockRequest(
            movement.CompanyId,
            warehouse,
            series.StockEffect == StockEffect.In ? StockDirection.In : StockDirection.Out,
            movement.MovementDate,
            movement.MovementType,
            movement.DocumentNumber,
            movement.Id,
            [.. movement.Lines.Select(line => new DocumentStockLine(
                line.Id,
                line.ProductCode,
                line.ProductDescription,
                line.Quantity))]);

        await stockRecorder.RecordAsync(request, userId, cancellationToken);
    }

    private string BuildQrCodePayload(StockMovement movement, string hash)
    {
        var taxAmounts = movement.Lines
            .GroupBy(line => new { line.TaxCountryRegion, line.TaxCode })
            .Select(group => new QrCodeTaxAmount(
                group.Key.TaxCountryRegion,
                group.Key.TaxCode,
                FiscalRounding.Amount(group.Sum(line => line.LineAmount)),
                FiscalRounding.Amount(group.Sum(line => line.TaxAmount))))
            .ToList();

        var fields = new QrCodeFields
        {
            IssuerTaxId = _fiscal.IssuerTaxId,
            BuyerTaxId = movement.PartyTaxId,
            DocumentType = movement.MovementType,
            DocumentStatus = movement.EffectiveStatus,
            DocumentDate = movement.MovementDate,
            DocumentNumber = movement.DocumentNumber,
            Atcud = movement.Atcud,
            TaxAmounts = taxAmounts,
            TotalTaxes = movement.TaxPayable,
            GrossTotal = movement.GrossTotal,
            HashCharacters = RsaDocumentSigner.ExtractPrintableHash(hash),
            CertificateNumber = _fiscal.CertificateNumber
        };

        return QrCodePayloadBuilder.Build(fields);
    }

    private static MovementParty ToParty(MovementPartyRequest party) =>
        new(string.IsNullOrWhiteSpace(party.TaxId) ? CustomerSnapshot.FinalConsumerTaxId : party.TaxId.Trim(),
            party.Name,
            party.IsSupplier);

    private static MovementLocation ToLocation(MovementLocationRequest location) =>
        new(location.Address.Trim(),
            Normalize(location.City),
            Normalize(location.PostalCode),
            location.Country,
            Normalize(location.WarehouseId),
            Normalize(location.LocationId));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);

    private static StockMovementDetailDto Map(StockMovement movement) =>
        new(movement.Id,
            movement.DocumentNumber,
            movement.MovementType,
            movement.Atcud,
            movement.MovementDate,
            movement.SystemEntryDateUtc,
            movement.EffectiveStatus,
            movement.PartyName,
            movement.PartyTaxId,
            movement.PartyIsSupplier,
            Map(movement.ShipFrom),
            Map(movement.ShipTo),
            movement.MovementStartAtUtc,
            movement.MovementEndAtUtc,
            movement.VehiclePlate,
            movement.Comments,
            movement.TotalQuantity,
            movement.NetTotal,
            movement.TaxPayable,
            movement.GrossTotal,
            RsaDocumentSigner.ExtractPrintableHash(movement.Hash),
            movement.QrCodePayload,
            movement.AtDocCodeId,
            movement.CommunicatedAtUtc,
            movement.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new StockMovementLineDto(
                    line.LineNumber,
                    line.ProductCode,
                    line.ProductDescription,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineAmount,
                    line.TaxCode,
                    line.TaxPercentage,
                    line.TaxAmount))
                .ToList());

    private static MovementLocationDto Map(MovementLocation location) =>
        new(location.Address,
            location.City,
            location.PostalCode,
            location.Country,
            location.WarehouseId,
            location.LocationId);
}
