namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

public sealed record AtTransportDocumentLine(
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice);
