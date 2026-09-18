namespace Erp.Common.Statements;

/// <summary>Which side of the account makes the balance grow.</summary>
public enum AccountBalanceSide
{
    /// <summary>A customer account: invoices are debits, and the balance is what they owe us.</summary>
    Debit = 0,

    /// <summary>A supplier account: invoices are credits, and the balance is what we owe them.</summary>
    Credit = 1
}
