using Erp.FiscalPT.Inventory;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;

namespace Erp.Inventory.Application.Services;

/// <summary>
/// Builds the inventory communication file. The quantities <b>and the values</b> come from the
/// ledger as it stood on the last day of the period — not from today's balances, which would report
/// the wrong stock, and the wrong cost, for any period already closed.
/// </summary>
/// <remarks>
/// The valuation is the weighted average of what was actually bought, replayed from the ledger. It
/// replaced the standard cost on the product file, which was a figure typed by hand and had no
/// necessary relationship to what the goods cost. The standard cost survives only as a fallback for
/// products the ledger never observed a cost for, and the result says how many of those there were.
/// </remarks>
public sealed class InventoryFileService(IStockStorage storage) : IInventoryFileService
{
    public async Task<InventoryFileResultDto> BuildAsync(
        InventoryFileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Company);

        if (string.IsNullOrWhiteSpace(request.Company.TaxId))
            throw new ArgumentException("The company has no tax id, which the file header requires.", nameof(request));

        var version = request.Valued ? InventoryFileVersion.Valued : InventoryFileVersion.QuantitiesOnly;

        var movements = await storage.GetMovementsAsync(request.CompanyId, request.EndDate, cancellationToken: cancellationToken);
        var stock = StockValuation.ReplayByProduct(movements);

        var products = request.Products.ToDictionary(x => x.ProductCode, StringComparer.Ordinal);
        var withoutCost = 0;
        var costedFromFile = 0;
        var negative = 0;
        var totalValue = 0m;
        var lines = new List<InventoryFileLine>();

        foreach (var held in stock)
        {
            // Nothing held means nothing to report, whichever way the balance got to zero.
            if (held.Quantity == 0)
                continue;

            if (held.Quantity < 0)
                negative++;

            products.TryGetValue(held.ProductCode, out var product);

            var value = Round(held.Value);

            // The ledger observed no cost for these goods — they came in through an adjustment or
            // before costing existed. The standard cost on the product file is then better than
            // nothing, and the caller is told how often it was needed.
            if (held.AverageCost == 0 && product is { UnitCost: > 0 })
            {
                value = Round(held.Quantity * product.UnitCost);
                costedFromFile++;
            }

            // Only worth flagging on the valued file, which is the one that has to carry a figure.
            if (value == 0 && request.Valued)
                withoutCost++;

            totalValue += value;

            lines.Add(new InventoryFileLine
            {
                ProductCategory = Category(product?.Category, version),
                ProductCode = held.ProductCode,
                // The ledger keeps the description of the last movement, which is what to fall
                // back on when the product is no longer in the file.
                ProductDescription = product?.Description ?? held.ProductDescription,
                ProductNumberCode = product?.Barcode ?? held.ProductCode,
                ClosingStockQuantity = held.Quantity,
                UnitOfMeasure = product?.UnitOfMeasure ?? "UN",
                ClosingStockValue = value
            });
        }

        var file = new InventoryFile
        {
            Version = version,
            Header = new InventoryFileHeader
            {
                TaxRegistrationNumber = OnlyDigits(request.Company.TaxId),
                FiscalYear = request.FiscalYear,
                EndDate = request.EndDate
            },
            Lines = lines
        };

        var document = InventoryXmlWriter.Build(file);

        // Validated here rather than only in the tests, so a file the writer was never exercised
        // on cannot reach the tax authority unnoticed.
        var validationErrors = InventorySchemaValidator.Validate(document, version);

        return new InventoryFileResultDto(
            InventoryXmlWriter.BuildFileName(file.Header),
            InventoryXmlWriter.Serialize(file),
            lines.Count,
            lines.Sum(line => line.ClosingStockQuantity),
            totalValue,
            withoutCost,
            costedFromFile,
            negative,
            validationErrors);
    }

    /// <summary>
    /// A category the schema does not know would fail validation, so it falls back to merchandise.
    /// The set differs by version: only the valued file accepts B, biological assets.
    /// </summary>
    private static string Category(string? category, InventoryFileVersion version) =>
        category is not null && InventoryConstants.IsKnownCategory(version, category)
            ? category
            : InventoryConstants.DefaultProductCategory;

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string OnlyDigits(string value) =>
        new([.. value.Where(char.IsDigit)]);
}
