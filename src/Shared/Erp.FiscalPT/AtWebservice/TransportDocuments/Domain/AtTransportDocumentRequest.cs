namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>
/// Everything the "Documentos de transporte" webservice needs for one document, shaped after the
/// WSDL's <c>StockMovement</c> element — deliberately not <c>Erp.Sales.Domain.StockMovement</c>
/// itself, since this library cannot depend on a business module.
/// </summary>
public sealed record AtTransportDocumentRequest(
    string IssuerTaxId,
    string CompanyName,
    AtTransportDocumentAddress CompanyAddress,
    string DocumentNumber,
    string? Atcud,
    string MovementStatus,
    DateOnly MovementDate,
    string MovementType,
    string PartyTaxId,
    bool PartyIsSupplier,
    string? PartyName,
    AtTransportDocumentAddress? ShipTo,
    AtTransportDocumentAddress ShipFrom,
    DateTime? MovementEndAtUtc,
    DateTime MovementStartAtUtc,
    string? VehiclePlate,
    IReadOnlyList<AtTransportDocumentLine> Lines);
