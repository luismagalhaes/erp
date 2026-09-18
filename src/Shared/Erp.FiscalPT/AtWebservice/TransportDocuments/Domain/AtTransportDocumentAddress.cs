namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>AT's AddressStructurePT: the country is always "PT", fixed by the schema itself.</summary>
public sealed record AtTransportDocumentAddress(string? AddressDetail, string? City, string? PostalCode);
