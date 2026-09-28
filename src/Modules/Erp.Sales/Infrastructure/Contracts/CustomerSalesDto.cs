namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>What one customer bought in a period, net of credit notes and without VAT.</summary>
public sealed record CustomerSalesDto(
    string CustomerName,
    string CustomerTaxId,
    decimal NetTotal,
    int DocumentCount);
