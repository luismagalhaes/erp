using Erp.FiscalPT.Documents;

namespace Erp.SeriesRegistry.Domain;

/// <summary>
/// The series a company needs before it can issue anything, and how they are named.
/// </summary>
/// <remarks>
/// A new company starts unable to issue a single document, because every document takes its number
/// from a series and there are none. Creating them one by one is twelve trips through a form to
/// arrive at the obvious answer, so the obvious answer is what a new company gets.
/// <para>
/// They still cannot issue: a series is only usable once the tax authority has returned its
/// validation code, and that is a step nobody can do on the company's behalf.
/// </para>
/// </remarks>
public static class StandardSeries
{
    /// <summary>
    /// Every document type numbered from a series: invoicing, goods movement and receipts. It is
    /// the same list the service validates against, deliberately — a type that can have a series
    /// gets one.
    /// </summary>
    public static readonly string[] DocumentTypes =
    [
        .. SalesDocumentTypes.All,
        .. MovementDocumentTypes.All,
        .. PaymentDocumentTypes.All
    ];

    /// <summary>
    /// The code a standard series carries: the document type followed by the year, as in
    /// <c>FT2026</c>. The year is in the code because a series does not roll over — a new one is
    /// opened each year, and the code is what tells them apart at a glance.
    /// </summary>
    public static string CodeFor(string documentType, int year) =>
        $"{documentType}{year}";
}
