namespace Erp.FiscalPT.AtWebservice;

/// <summary>
/// The WDT subutilizador that authenticates the SOAP call, per company: AT's Username field is
/// built as "&lt;TaxRegistrationNumber&gt;/&lt;SubUserId&gt;".
/// </summary>
public sealed record AtCredentials(string TaxRegistrationNumber, string SubUserId, string Password);
