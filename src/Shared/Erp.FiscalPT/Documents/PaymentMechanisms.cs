namespace Erp.FiscalPT.Documents;

/// <summary>
/// SAF-T (PT) PaymentMechanism codes: how the money actually moved. A receipt may combine
/// several, for example part in cash and part by card.
/// </summary>
public static class PaymentMechanisms
{
    /// <summary>Numerário.</summary>
    public const string Cash = "NU";

    /// <summary>Cheque.</summary>
    public const string Cheque = "CH";

    /// <summary>Cartão de débito.</summary>
    public const string DebitCard = "CD";

    /// <summary>Cartão de crédito.</summary>
    public const string CreditCard = "CC";

    /// <summary>Transferência bancária.</summary>
    public const string BankTransfer = "TB";

    /// <summary>Débito direto.</summary>
    public const string DirectDebit = "DE";

    /// <summary>Cartão oferta.</summary>
    public const string GiftCard = "CO";

    /// <summary>Compensação de saldos em conta corrente.</summary>
    public const string AccountBalance = "CS";

    /// <summary>Letra comercial.</summary>
    public const string CommercialBill = "LC";

    /// <summary>Outros.</summary>
    public const string Other = "OU";

    public static readonly string[] All =
    [
        Cash, Cheque, DebitCard, CreditCard, BankTransfer,
        DirectDebit, GiftCard, AccountBalance, CommercialBill, Other
    ];

    public static bool IsSupported(string mechanism) =>
        All.Contains(mechanism, StringComparer.Ordinal);
}
