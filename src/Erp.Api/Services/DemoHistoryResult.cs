namespace Erp.Api.Services;

/// <summary>What the demo history created for a company, or that it had already been created.</summary>
public sealed record DemoHistoryResult(
    bool Applied,
    int Invoices,
    int CreditNotes,
    int VoidedInvoices,
    int PurchaseInvoices);
