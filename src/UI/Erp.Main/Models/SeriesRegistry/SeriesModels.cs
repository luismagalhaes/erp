namespace Erp.Main.Models.SeriesRegistry;

public sealed record SalesSeries(
    Guid Id,
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int CurrentSequence,
    string? ValidationCode,
    string Status,
    bool CanIssue,
    string StockEffect = "None",
    bool SelfBilling = false,
    DateTime? CommunicatedAtUtc = null,
    DateTime? FinalizedAtUtc = null,
    DateTime? CancelledAtUtc = null);

public sealed record CreateSeriesRequest(
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int InitialSequence = 1,
    string? EstablishmentCode = null,
    string? StockEffect = null,
    bool SelfBilling = false);

public sealed record FinalizeSeriesRequest(string? Justificacao = null);

public sealed record CommunicateSeriesManuallyRequest(string ValidationCode);

/// <summary>
/// The only field a series still allows to be changed. Everything else was communicated to the AT
/// or is already written into the documents issued from it.
/// </summary>
public sealed record UpdateSeriesRequest(string StockEffect);

/// <summary>What a document series does to stock, for the UI selects.</summary>
public static class StockEffects
{
    public static readonly (string Code, string Label)[] All =
    [
        ("None", "Não movimenta"),
        ("In", "Entrada"),
        ("Out", "Saída")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(effect => effect.Code == code).Label ?? code;
}
