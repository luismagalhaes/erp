namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="EndDate">
/// Last day of the period. The stock reported is the stock held on this date, which is not the
/// same as the stock today.
/// </param>
/// <param name="Products">
/// The product file, for valuation and classification. Products with no stock are left out of the
/// file, so supplying more than needed is harmless.
/// </param>
/// <param name="Valued">
/// True for the valued file (schema 2_01), which carries the stock value and is what the tax
/// authority requires for periods from 2021 onwards. False for the older quantities-only file.
/// </param>
public sealed record InventoryFileRequest(
    Guid CompanyId,
    int FiscalYear,
    DateOnly EndDate,
    InventoryCompanyInfo Company,
    IReadOnlyList<InventoryFileProductDto> Products,
    bool Valued = true);
