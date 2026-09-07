using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// Purchase orders. Nothing here is a fiscal document — no series, no signature, no ATCUD — so an
/// order can be corrected in place, which is the opposite of how <c>Erp.Sales</c> works and worth
/// keeping in mind when reading both.
/// </summary>
public sealed class PurchaseOrderService(IPurchaseOrderStorage storage) : IPurchaseOrderService
{
    /// <summary>Prefix of the order number. Ours, with no fiscal meaning.</summary>
    private const string NumberPrefix = "ENC";

    public async Task<IReadOnlyList<PurchaseOrderListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var orders = await storage.GetAllAsync(companyId, supplierId, openOnly, cancellationToken);
        return [.. orders.Select(MapListItem)];
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await storage.GetByIdAsync(id, cancellationToken);
        return order is null ? null : Map(order);
    }

    public async Task<IReadOnlyList<PendingOrderLineDto>> GetPendingLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var orders = await storage.GetPendingAsync(companyId, supplierId, cancellationToken);

        return
        [
            .. orders
                .SelectMany(order => order.Lines
                    .Where(line => line.PendingQuantity > 0)
                    .Select(line => new PendingOrderLineDto(
                        order.Id,
                        order.Number,
                        order.OrderDate,
                        order.ExpectedDate,
                        order.SupplierId,
                        order.Supplier.Name,
                        order.WarehouseId,
                        line.Id,
                        line.ProductCode,
                        line.ProductDescription,
                        line.UnitOfMeasure,
                        line.Quantity,
                        line.ReceivedQuantity,
                        line.PendingQuantity,
                        line.UnitPrice)))
                .OrderBy(line => line.ExpectedDate ?? DateOnly.MaxValue)
                .ThenBy(line => line.OrderNumber, StringComparer.Ordinal)
        ];
    }

    public async Task<PurchaseOrderDto> CreateAsync(
        CreatePurchaseOrderRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);
        ArgumentNullException.ThrowIfNull(request.Lines);

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("An order has to say who it is being placed with.", nameof(request));

        EnsureDeliverable(request.OrderDate, request.ExpectedDate);

        var number = await NextNumberAsync(request.CompanyId, request.OrderDate.Year, cancellationToken);

        var order = PurchaseOrder.Create(
            request.CompanyId,
            request.SupplierId,
            ToSnapshot(request.Supplier),
            number,
            request.OrderDate,
            request.WarehouseId,
            [.. request.Lines.Select(ToLine)],
            request.ExpectedDate,
            request.Notes,
            userId);

        if (request.Place)
            order.Place();

        await storage.AddAsync(order, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PurchaseOrderDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Lines);

        var order = await storage.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return null;

        EnsureDeliverable(request.OrderDate, request.ExpectedDate);

        order.UpdateHeader(request.OrderDate, request.ExpectedDate, request.WarehouseId, request.Notes);
        order.ReplaceLines([.. request.Lines.Select(ToLine)]);

        await storage.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PurchaseOrderDto?> PlaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await storage.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return null;

        order.Place();
        await storage.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PurchaseOrderDto?> CloseAsync(
        Guid id,
        ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await storage.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return null;

        order.Close(request.Reason, DateTime.UtcNow);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<PurchaseOrderDto?> CancelAsync(
        Guid id,
        ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await storage.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return null;

        order.Cancel(request.Reason, DateTime.UtcNow);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    /// <summary>
    /// Next number for the company and year, as <c>ENC2026/7</c>. Unlike a fiscal series this needs
    /// no row lock: a collision is caught by the unique index and a gap costs nothing, because the
    /// number means nothing to anyone but us.
    /// </summary>
    private async Task<string> NextNumberAsync(Guid companyId, int year, CancellationToken cancellationToken)
    {
        var sequence = await storage.GetLastSequenceAsync(companyId, year, cancellationToken) + 1;
        var number = $"{NumberPrefix}{year}/{sequence}";

        while (await storage.NumberExistsAsync(companyId, number, cancellationToken))
        {
            sequence++;
            number = $"{NumberPrefix}{year}/{sequence}";
        }

        return number;
    }

    /// <summary>Goods cannot be expected before they were ordered.</summary>
    private static void EnsureDeliverable(DateOnly orderDate, DateOnly? expectedDate)
    {
        if (expectedDate is { } expected && expected < orderDate)
            throw new ArgumentException("The expected date is before the order date.", nameof(expectedDate));
    }

    private static PurchaseOrderLine ToLine(PurchaseOrderLineRequest request) => new()
    {
        ProductCode = request.ProductCode.Trim(),
        ProductDescription = request.ProductDescription.Trim(),
        Quantity = request.Quantity,
        UnitOfMeasure = request.UnitOfMeasure,
        UnitPrice = request.UnitPrice,
        TaxCountryRegion = request.TaxCountryRegion,
        TaxCode = request.TaxCode,
        TaxPercentage = request.TaxPercentage
    };

    private static SupplierSnapshot ToSnapshot(PurchaseOrderSupplierDto supplier) =>
        new(supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country);

    private static PurchaseOrderListItemDto MapListItem(PurchaseOrder order) =>
        new(order.Id,
            order.Number,
            order.Status.ToString(),
            order.OrderDate,
            order.ExpectedDate,
            order.Supplier.Name,
            order.Supplier.TaxId,
            order.Lines.Count,
            order.GrossTotal,
            order.IsOpen);

    private static PurchaseOrderDto Map(PurchaseOrder order) =>
        new(order.Id,
            order.CompanyId,
            order.SupplierId,
            order.Number,
            order.Status.ToString(),
            order.OrderDate,
            order.ExpectedDate,
            order.WarehouseId,
            new PurchaseOrderSupplierDto(
                order.Supplier.Code,
                order.Supplier.Name,
                order.Supplier.TaxId,
                order.Supplier.Address,
                order.Supplier.PostalCode,
                order.Supplier.City,
                order.Supplier.Country),
            order.Notes,
            order.NetTotal,
            order.TaxTotal,
            order.GrossTotal,
            order.CreatedAtUtc,
            order.ClosedAtUtc,
            order.ClosedReason,
            [.. order.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new PurchaseOrderLineDto(
                    line.Id,
                    line.LineNumber,
                    line.ProductCode,
                    line.ProductDescription,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.LineAmount,
                    line.TaxCountryRegion,
                    line.TaxCode,
                    line.TaxPercentage,
                    line.TaxAmount,
                    line.ReceivedQuantity,
                    line.PendingQuantity))]);
}
