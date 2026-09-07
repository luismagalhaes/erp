namespace Erp.Main.Models;

public sealed record CreatePaymentLineRequest(Guid OriginatingDocumentId, decimal AppliedAmount);

public sealed record CreatePaymentMethodRequest(string Mechanism, decimal Amount, DateOnly PaymentDate);

public sealed record CreatePaymentRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly TransactionDate,
    string? PartyTaxId,
    string PartyName,
    IReadOnlyList<CreatePaymentLineRequest> Lines,
    IReadOnlyList<CreatePaymentMethodRequest> Methods,
    string? Description = null);

public sealed record VoidPaymentRequest(string Reason);

public sealed record OutstandingInvoice(
    Guid DocumentId,
    string DocumentNumber,
    DateOnly DocumentDate,
    string CustomerName,
    string CustomerTaxId,
    decimal GrossTotal,
    decimal SettledAmount,
    decimal OutstandingAmount);

public sealed record PaymentListItem(
    Guid Id,
    string PaymentRefNo,
    string PaymentType,
    string Atcud,
    DateOnly TransactionDate,
    string PartyName,
    string PartyTaxId,
    decimal GrossTotal,
    int SettledInvoiceCount,
    string Status);

public sealed record PaymentLine(
    int LineNumber,
    Guid OriginatingDocumentId,
    string OriginatingNumber,
    DateOnly OriginatingDate,
    decimal AppliedAmount);

public sealed record PaymentMethod(string Mechanism, decimal Amount, DateOnly PaymentDate);

public sealed record PaymentDetail(
    Guid Id,
    string PaymentRefNo,
    string PaymentType,
    string Atcud,
    DateOnly TransactionDate,
    DateTime SystemEntryDateUtc,
    string Status,
    string PartyName,
    string PartyTaxId,
    string? Description,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string PrintableHash,
    string QrCodePayload,
    IReadOnlyList<PaymentLine> Lines,
    IReadOnlyList<PaymentMethod> Methods);

/// <summary>Receipt types, for the UI selects.</summary>
public static class PaymentTypes
{
    public static readonly (string Code, string Label)[] All =
    [
        ("RC", "Recibo (regime de IVA de caixa)"),
        ("RG", "Outros recibos")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(type => type.Code == code).Label ?? code;
}

/// <summary>SAF-T payment mechanisms, for the UI selects.</summary>
public static class PaymentMechanismOptions
{
    public static readonly (string Code, string Label)[] All =
    [
        ("NU", "Numerário"),
        ("CH", "Cheque"),
        ("CD", "Cartão de débito"),
        ("CC", "Cartão de crédito"),
        ("TB", "Transferência bancária"),
        ("DE", "Débito directo"),
        ("CO", "Cheque ou cartão oferta"),
        ("CS", "Compensação de saldos em conta corrente"),
        ("LC", "Letra comercial"),
        ("OU", "Outro")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(mechanism => mechanism.Code == code).Label ?? code;
}
