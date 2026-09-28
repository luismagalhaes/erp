namespace Erp.Main.Models.Core;

/// <summary>What the demo history created for the company, or that it was already there.</summary>
public sealed record DemoHistoryResult(
    bool Applied,
    int Invoices,
    int CreditNotes,
    int VoidedInvoices,
    int PurchaseInvoices);
