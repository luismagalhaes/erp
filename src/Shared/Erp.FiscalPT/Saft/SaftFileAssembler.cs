namespace Erp.FiscalPT.Saft;

/// <summary>
/// Puts one SAF-T file together from what the modules contributed. Pure: it merges, it does not
/// read anything.
/// </summary>
public static class SaftFileAssembler
{
    /// <summary>
    /// Merges the contributions under one header. Master files are deduplicated by their key, so
    /// two modules that both know the same customer produce one entry, not two — which is what the
    /// schema's uniqueness constraints demand.
    /// </summary>
    public static SaftAuditFile Assemble(SaftHeader header, IEnumerable<SaftSourceContent> contents)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(contents);

        var all = contents.ToList();

        return new SaftAuditFile
        {
            Header = header,
            Customers = [.. all.SelectMany(x => x.Customers)
                .GroupBy(customer => customer.CustomerId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(customer => customer.CustomerId, StringComparer.Ordinal)],
            Products = [.. all.SelectMany(x => x.Products)
                .GroupBy(product => product.ProductCode, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(product => product.ProductCode, StringComparer.Ordinal)],
            TaxTable = [.. all.SelectMany(x => x.TaxTable)
                .GroupBy(entry => (entry.TaxCountryRegion, entry.TaxCode, entry.TaxPercentage))
                .Select(group => group.First())
                .OrderBy(entry => entry.TaxCode, StringComparer.Ordinal)
                .ThenBy(entry => entry.TaxPercentage)],
            // Documents keep the order their source gave them; the writer computes the totals.
            Invoices = [.. all.SelectMany(x => x.Invoices)],
            StockMovements = [.. all.SelectMany(x => x.StockMovements)],
            Payments = [.. all.SelectMany(x => x.Payments)]
        };
    }
}
