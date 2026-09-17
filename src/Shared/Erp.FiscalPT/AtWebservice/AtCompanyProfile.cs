namespace Erp.FiscalPT.AtWebservice;

/// <summary>
/// What the transport-documents webservice needs about the issuing company: its own tax identity
/// (this call's NIF has to match the credential's NIF, or AT rejects it with error -7) and the WDT
/// subutilizador credentials that company registered at the Portal das Finanças.
/// </summary>
public sealed record AtCompanyProfile(
    string TaxId,
    string CompanyName,
    string? Address,
    string? City,
    string? PostalCode,
    string SubUserId,
    string Password);
