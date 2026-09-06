using Erp.FiscalPT.Documents;
using Erp.FiscalPT.QrCode;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class QrCodePayloadBuilderTests
{
    private static QrCodeFields Fields(params QrCodeTaxAmount[] taxAmounts) => new()
    {
        IssuerTaxId = "123456789",
        BuyerTaxId = "999999990",
        DocumentType = SalesDocumentTypes.Invoice,
        DocumentStatus = DocumentStatuses.Normal,
        DocumentDate = new DateOnly(2026, 1, 15),
        DocumentNumber = "FT A2026/2",
        Atcud = "JFTX7RK9-2",
        TaxAmounts = taxAmounts,
        TotalTaxes = 46.75m,
        GrossTotal = 250.00m,
        HashCharacters = "kR9x",
        CertificateNumber = "9999"
    };

    [Fact]
    public void Build_writes_the_structural_fields_in_order()
    {
        var payload = QrCodePayloadBuilder.Build(Fields());

        payload.Should().StartWith("A:123456789*B:999999990*C:PT*D:FT*E:N*F:20260115*G:FT A2026/2*H:JFTX7RK9-2");
        payload.Should().EndWith("N:46.75*O:250.00*Q:kR9x*R:9999");
    }

    [Fact]
    public void Build_maps_mainland_rates_to_the_I_block()
    {
        var payload = QrCodePayloadBuilder.Build(Fields(
            new QrCodeTaxAmount(TaxCountryRegions.Mainland, TaxCodes.Normal, 203.25m, 46.75m)));

        payload.Should().Contain("I1:PT");
        payload.Should().Contain("I7:203.25");
        payload.Should().Contain("I8:46.75");
    }

    [Fact]
    public void Build_maps_each_fiscal_space_to_its_own_block()
    {
        var payload = QrCodePayloadBuilder.Build(Fields(
            new QrCodeTaxAmount(TaxCountryRegions.Mainland, TaxCodes.Reduced, 100m, 6m),
            new QrCodeTaxAmount(TaxCountryRegions.Azores, TaxCodes.Normal, 200m, 32m)));

        payload.Should().Contain("I1:PT");
        payload.Should().Contain("I3:100.00");
        payload.Should().Contain("I4:6.00");
        payload.Should().Contain("J1:PT-AC");
        payload.Should().Contain("J7:200.00");
        payload.Should().Contain("J8:32.00");
    }

    [Fact]
    public void Build_writes_the_exempt_base_without_a_tax_amount()
    {
        var payload = QrCodePayloadBuilder.Build(Fields(
            new QrCodeTaxAmount(TaxCountryRegions.Mainland, TaxCodes.Exempt, 80m, 0m)));

        payload.Should().Contain("I2:80.00");
        payload.Should().NotContain("I3:");
    }

    [Fact]
    public void Build_rejects_an_unknown_fiscal_space()
    {
        var act = () => QrCodePayloadBuilder.Build(Fields(
            new QrCodeTaxAmount("ES", TaxCodes.Normal, 100m, 23m)));

        act.Should().Throw<ArgumentException>();
    }
}
