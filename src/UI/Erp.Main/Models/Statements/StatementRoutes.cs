using Erp.Common;

namespace Erp.Main.Models.Statements;

/// <summary>Where each line of a current account statement opens in this app.</summary>
public static class StatementRoutes
{
    public static string? ForSource(string source, Guid sourceId) => source switch
    {
        Constants.StatementSources.SalesDocument => $"invoices/{sourceId}",
        Constants.StatementSources.Receipt => $"payments/{sourceId}",
        Constants.StatementSources.PurchaseInvoice => $"supplier-invoices/{sourceId}",
        Constants.StatementSources.SelfBilledInvoice => $"self-billed-invoices/{sourceId}",
        Constants.StatementSources.SupplierPayment => $"supplier-payments/{sourceId}",
        _ => null
    };

    /// <summary>A statement page with its party and period in the query string, so it can be linked to.</summary>
    public static string WithQuery(string route, string partyParameter, Guid partyId, DateOnly? startDate, DateOnly? endDate) =>
        $"{route}?{partyParameter}={partyId}"
        + (startDate is { } start ? $"&startDate={start:yyyy-MM-dd}" : string.Empty)
        + (endDate is { } end ? $"&endDate={end:yyyy-MM-dd}" : string.Empty);
}
