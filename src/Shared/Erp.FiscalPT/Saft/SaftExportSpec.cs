namespace Erp.FiscalPT.Saft;

/// <summary>What file to produce: which kind, for whom, and over what period.</summary>
/// <param name="FileType">See <see cref="SaftFileType"/>. Decides which sources are asked.</param>
/// <param name="Subject">
/// The entity the file is about. The company on a billing file; the supplier on a self-billing one.
/// </param>
/// <param name="SelfBiller">
/// The company, when it issued the documents on the subject's behalf. Required for a self-billing
/// file and meaningless on any other: it is the customer of the sales the file reports.
/// </param>
public sealed record SaftExportSpec(
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    string FileType,
    SaftEntityInfo Subject,
    SaftProducerInfo Producer,
    SaftEntityInfo? SelfBiller = null);
