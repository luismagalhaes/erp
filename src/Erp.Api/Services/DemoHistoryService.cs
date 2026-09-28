using System.Security.Cryptography;
using System.Text;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Contracts;

namespace Erp.Api.Services;

/// <summary>
/// Gives a company two years of sales and purchases, issued through the real services, so the
/// figures the assistant and the reports read have something to say.
/// </summary>
/// <remarks>
/// Composed in the host for the same reason <see cref="DemoDataService"/> is: it draws from the
/// series registry, Sales and Purchasing, and none of them knows about the others. Everything goes
/// through the ordinary issuing path — numbering, signing, hash chain — rather than into the tables,
/// so what comes out is what the application itself would have produced.
/// <para>
/// The numbers are not random noise. They carry a seasonal curve, growth year on year, one weak
/// month this year, a promotion last year, two customers that stopped buying and one that arrived,
/// so a question like "why are sales down?" has an answer to find. The generator is seeded, so the
/// same company always gets the same history.
/// </para>
/// It uses series of its own, with no stock effect: the sales are not backed by purchases in the
/// warehouse, and this must not drive the company's stock negative. Purchases are recorded as
/// services and other goods for the same reason, so they bring nothing into stock.
/// </remarks>
public sealed class DemoHistoryService(
    ISeriesService seriesService,
    ISalesDocumentService salesDocumentService,
    IPurchaseInvoiceService purchaseInvoiceService,
    ILogger<DemoHistoryService> logger)
{
    private const int MonthsOfHistory = 24;
    private const string SeriesPrefix = "HIST";
    private const string FakeValidationCode = "XXXX";
    private const string NoStockEffect = "None";
    private const string CreditNoteReason = "Devolução de mercadoria";
    private const decimal VatPercentage = 23m;

    private const int SalesPerMonth = 12;
    private const int PurchasesPerMonth = 6;
    private const int CreditNoteEvery = 11;
    private const int VoidEvery = 29;

    /// <summary>Sales by calendar month, January first: a workshop supplier is busiest in spring and autumn.</summary>
    private static readonly double[] Seasonality =
        [0.85, 0.90, 1.05, 1.10, 1.15, 1.10, 1.00, 0.60, 1.05, 1.20, 1.15, 1.00];

    private static readonly (string Code, string Description, decimal Price, int MaxQuantity)[] Products =
    [
        ("HIST-001", "Jogo de Pastilhas de Travão", 34.50m, 4),
        ("HIST-002", "Disco de Travão Ventilado", 48.00m, 4),
        ("HIST-003", "Correia de Distribuição", 29.90m, 3),
        ("HIST-004", "Vela de Ignição", 8.50m, 8),
        ("HIST-005", "Amortecedor Dianteiro", 58.00m, 4),
        ("HIST-006", "Rótula de Direção", 19.50m, 4),
        ("HIST-007", "Bateria 12V 60Ah", 99.00m, 2),
        ("HIST-008", "Alternador", 145.00m, 1),
        ("HIST-009", "Filtro de Óleo", 7.50m, 12),
        ("HIST-010", "Filtro de Ar", 9.90m, 10),
        ("HIST-011", "Kit de Embraiagem", 185.00m, 1),
        ("HIST-012", "Bomba de Água", 42.00m, 2),
        ("HIST-013", "Lâmpada H7 (par)", 12.90m, 6),
        ("HIST-014", "Escovas Limpa-Vidros", 16.50m, 4),
        ("HIST-015", "Óleo Motor 5W30 5L", 39.00m, 6),
        ("HIST-016", "Líquido de Refrigeração", 12.00m, 6)
    ];

    /// <summary>Customers, with how much each one buys and the months it is a customer for.</summary>
    private static readonly (string Name, string TaxIdBase, int Weight, DateOnly? From, DateOnly? Until)[] Customers =
    [
        ("Oficina Central de Lisboa, Lda", "50210001", 25, null, null),
        ("Auto Peças do Vouga, SA", "50210002", 18, null, null),
        ("Garagem Bela Vista, Lda", "50210003", 14, null, null),
        ("Frotas Ibéricas, SA", "50210004", 12, null, null),
        ("Mecânica Rápida do Porto, Lda", "50210005", 10, null, null),
        ("Auto Serviço Alentejano, Lda", "50210006", 8, null, null),
        ("Reparações Silva & Filhos, Lda", "50210007", 6, null, null),
        ("Táxis Costa Azul, Lda", "50210008", 4, null, null),
        // Stopped buying: the ones an assistant asked "who left?" should find.
        ("Transportes Rodoviários do Norte, SA", "50210009", 8, null, new DateOnly(2025, 9, 30)),
        ("Oficina Automóvel Marques, Lda", "50210010", 6, null, new DateOnly(2025, 12, 31)),
        // Arrived this year.
        ("Concessionária Atlântico, SA", "50210011", 10, new DateOnly(2026, 2, 1), null)
    ];

    private static readonly (string Code, string Name, string TaxIdBase)[] Suppliers =
    [
        ("F001", "Autodoc Distribuição, Lda", "50310001"),
        ("F002", "Peças e Componentes do Norte, SA", "50310002"),
        ("F003", "Lubrificantes do Tejo, Lda", "50310003"),
        ("F004", "Baterias Ibéria, SA", "50310004"),
        ("F005", "Filtros e Travões Europa, Lda", "50310005"),
        ("F006", "Eletro Auto Sul, Lda", "50310006")
    ];

    /// <summary>
    /// Creates the history, unless it was already created for this company — in which case nothing
    /// is issued again.
    /// </summary>
    /// <param name="today">
    /// The last day of the history. Passed in rather than read, so the same company gets the same
    /// history whenever it is asked for, and so it can be tested.
    /// </param>
    public async Task<DemoHistoryResult> ApplyAsync(
        Guid companyId,
        string? userId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-(MonthsOfHistory - 1));

        var existing = await seriesService.GetAllAsync(companyId, cancellationToken);

        if (existing.Any(series => series.SeriesCode == SeriesCode(first.Year)))
        {
            logger.LogInformation("Demo history was already applied to company {CompanyId}; nothing to do.", companyId);
            return new DemoHistoryResult(false, 0, 0, 0, 0);
        }

        var series = await CreateSeriesAsync(companyId, first.Year, today.Year, userId, cancellationToken);
        var random = new Random(companyId.GetHashCode());

        var invoices = 0;
        var creditNotes = 0;
        var voided = 0;
        var purchases = 0;
        var purchaseNumbers = new int[Suppliers.Length];

        for (var month = first; month <= today; month = month.AddMonths(1))
        {
            var lastDay = month.Year == today.Year && month.Month == today.Month
                ? today.Day
                : DateTime.DaysInMonth(month.Year, month.Month);

            var monthsIn = (month.Year - first.Year) * 12 + month.Month - first.Month;
            var share = lastDay / (double)DateTime.DaysInMonth(month.Year, month.Month);

            foreach (var day in Days(random, SalesCount(month, monthsIn, today, share), lastDay))
            {
                var date = new DateOnly(month.Year, month.Month, day);
                var invoice = await IssueInvoiceAsync(companyId, series[(month.Year, "FT")], date, random, userId, cancellationToken);

                invoices++;

                if (invoices % VoidEvery == 0)
                {
                    await salesDocumentService.VoidAsync(invoice.Invoice.Id, "Emitida por engano", userId, cancellationToken);
                    voided++;
                }
                else if (invoices % CreditNoteEvery == 0)
                {
                    await IssueCreditNoteAsync(companyId, series[(month.Year, "NC")], date, invoice, userId, cancellationToken);
                    creditNotes++;
                }
            }

            foreach (var day in Days(random, PurchasesCount(month, monthsIn, share), lastDay))
            {
                var supplierIndex = random.Next(Suppliers.Length);
                var date = new DateOnly(month.Year, month.Month, day);

                await RecordPurchaseAsync(
                    companyId, supplierIndex, ++purchaseNumbers[supplierIndex], date, today, random, userId, cancellationToken);

                purchases++;
            }
        }

        logger.LogInformation(
            "Applied demo history to company {CompanyId}: {Invoices} invoices, {CreditNotes} credit notes, {Voided} voided, {Purchases} purchase invoices.",
            companyId, invoices, creditNotes, voided, purchases);

        return new DemoHistoryResult(true, invoices, creditNotes, voided, purchases);
    }

    /// <summary>One invoice and credit note series per year, communicated with the same fake code the demo data uses.</summary>
    private async Task<Dictionary<(int Year, string DocumentType), Guid>> CreateSeriesAsync(
        Guid companyId, int fromYear, int toYear, string? userId, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(int, string), Guid>();

        for (var year = fromYear; year <= toYear; year++)
        {
            foreach (var documentType in new[] { "FT", "NC" })
            {
                var created = await seriesService.CreateAsync(
                    new CreateSeriesRequest(companyId, documentType, SeriesCode(year), StockEffect: NoStockEffect),
                    userId,
                    cancellationToken);

                await seriesService.CommunicateManuallyAsync(created.Id, FakeValidationCode, cancellationToken);

                result[(year, documentType)] = created.Id;
            }
        }

        return result;
    }

    private async Task<IssuedInvoice> IssueInvoiceAsync(
        Guid companyId,
        Guid seriesId,
        DateOnly date,
        Random random,
        string? userId,
        CancellationToken cancellationToken)
    {
        var customer = PickCustomer(date, random);
        var lines = new List<CreateInvoiceLineRequest>();

        for (var i = random.Next(1, 5); i > 0; i--)
        {
            var product = Products[random.Next(Products.Length)];
            var price = Math.Round(product.Price * (decimal)(0.97 + random.NextDouble() * 0.06), 2);

            lines.Add(new CreateInvoiceLineRequest(
                product.Code, product.Description, random.Next(1, product.MaxQuantity + 1), price, "NOR", VatPercentage));
        }

        var invoice = await salesDocumentService.IssueAsync(
            new CreateInvoiceRequest(
                companyId,
                seriesId,
                date,
                new CustomerRequest(TaxId(customer.TaxIdBase), customer.Name, "Zona Industrial", PostalCode: "1000-001", City: "Lisboa"),
                lines,
                DueDate: date.AddDays(30)),
            userId,
            cancellationToken);

        return new IssuedInvoice(invoice, lines[0]);
    }

    /// <summary>Credits one unit of the invoice's first line: always less than the invoice, so it always fits.</summary>
    private async Task IssueCreditNoteAsync(
        Guid companyId,
        Guid seriesId,
        DateOnly date,
        IssuedInvoice invoice,
        string? userId,
        CancellationToken cancellationToken)
    {
        var line = invoice.FirstLine with { Quantity = 1m };

        await salesDocumentService.IssueAsync(
            new CreateInvoiceRequest(
                companyId,
                seriesId,
                date,
                new CustomerRequest(invoice.Invoice.CustomerTaxId, invoice.Invoice.CustomerName, invoice.Invoice.CustomerAddress),
                [line],
                RectifiedDocumentId: invoice.Invoice.Id,
                RectificationReason: CreditNoteReason),
            userId,
            cancellationToken);
    }

    private async Task RecordPurchaseAsync(
        Guid companyId,
        int supplierIndex,
        int sequence,
        DateOnly date,
        DateOnly today,
        Random random,
        string? userId,
        CancellationToken cancellationToken)
    {
        var (code, name, taxIdBase) = Suppliers[supplierIndex];
        var taxId = TaxId(taxIdBase);
        var lines = new List<PurchaseInvoiceLineRequest>();

        for (var i = random.Next(1, 4); i > 0; i--)
        {
            var product = Products[random.Next(Products.Length)];
            var cost = Math.Round(product.Price * (decimal)(0.60 + random.NextDouble() * 0.08), 2);

            lines.Add(new PurchaseInvoiceLineRequest(
                product.Code,
                product.Description,
                random.Next(4, 31),
                cost,
                DeductionNature: "OtherGoodsAndServices"));
        }

        var received = date.AddDays(random.Next(1, 4));

        await purchaseInvoiceService.RecordAsync(
            new RecordPurchaseInvoiceRequest(
                companyId,
                SupplierId(companyId, taxId),
                new PurchaseOrderSupplierDto(code, name, taxId, "Zona Industrial", "4000-001", "Porto"),
                "FT",
                $"FT {date.Year}/{sequence}",
                date,
                received > today ? today : received,
                lines,
                DueDate: date.AddDays(45)),
            userId,
            cancellationToken);
    }

    private static int SalesCount(DateOnly month, int monthsIn, DateOnly today, double share)
    {
        var count = SalesPerMonth * Seasonality[month.Month - 1] * (1 + 0.008 * monthsIn);

        // A weak month this year — customers lost and a slow spring — and a promotion the year before.
        if (month.Year == today.Year && month.Month == 3)
            count *= 0.55;

        if (month.Year == today.Year - 1 && month.Month == 11)
            count *= 1.5;

        return Math.Max(1, (int)Math.Round(count * share));
    }

    /// <summary>Purchases lead sales by a month: stock is bought before it is sold.</summary>
    private static int PurchasesCount(DateOnly month, int monthsIn, double share)
    {
        var next = Seasonality[month.AddMonths(1).Month - 1];

        return Math.Max(1, (int)Math.Round(PurchasesPerMonth * next * (1 + 0.008 * monthsIn) * share));
    }

    private static (string Name, string TaxIdBase, int Weight, DateOnly? From, DateOnly? Until) PickCustomer(DateOnly date, Random random)
    {
        var active = Customers
            .Where(customer => (customer.From is null || customer.From <= date) && (customer.Until is null || customer.Until >= date))
            .ToList();

        var roll = random.Next(active.Sum(customer => customer.Weight));

        foreach (var customer in active)
        {
            roll -= customer.Weight;

            if (roll < 0)
                return customer;
        }

        return active[^1];
    }

    /// <summary>Distinct days of the month, in order: a series is issued chronologically.</summary>
    private static IEnumerable<int> Days(Random random, int count, int lastDay) =>
        Enumerable.Range(1, lastDay).OrderBy(_ => random.Next()).Take(Math.Min(count, lastDay)).Order();

    /// <summary>Completes an 8 digit base with the modulo 11 check digit of a Portuguese NIF.</summary>
    private static string TaxId(string eightDigits)
    {
        var sum = eightDigits.Select((digit, index) => (digit - '0') * (9 - index)).Sum();
        var check = 11 - sum % 11;

        return eightDigits + (check >= 10 ? 0 : check);
    }

    /// <summary>Stable per company and supplier, so re-running never scatters one supplier over several ids.</summary>
    private static Guid SupplierId(Guid companyId, string taxId) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{companyId}:{taxId}")));

    private static string SeriesCode(int year) => $"{SeriesPrefix}{year}";

    private sealed record IssuedInvoice(InvoiceDetailDto Invoice, CreateInvoiceLineRequest FirstLine);
}
