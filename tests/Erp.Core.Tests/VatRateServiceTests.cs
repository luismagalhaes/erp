using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class VatRateServiceTests
{
    private readonly IVatRateStorage _storage = Substitute.For<IVatRateStorage>();

    private VatRateService CreateService() => new(_storage);

    [Fact]
    public async Task GetAllAsync_maps_every_rate()
    {
        var rate = new VatRate
        {
            FiscalRegion = "PT",
            Code = "NOR",
            Label = "Normal",
            Percentage = 23m,
            IsActive = true
        };
        _storage.GetAllAsync(Arg.Any<CancellationToken>()).Returns([rate]);

        var result = await CreateService().GetAllAsync();

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new VatRateDto(
            rate.Id, "PT", "NOR", "Normal", 23m, true));
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_an_unknown_rate()
    {
        var result = await CreateService().UpdateAsync(Guid.NewGuid(), new UpdateVatRateRequest(20m, true));

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_changes_the_percentage_and_active_flag()
    {
        var rate = new VatRate { FiscalRegion = "PT-AC", Code = "NOR", Label = "Normal", Percentage = 16m, IsActive = true };
        _storage.GetByIdAsync(rate.Id, Arg.Any<CancellationToken>()).Returns(rate);

        var result = await CreateService().UpdateAsync(rate.Id, new UpdateVatRateRequest(18m, false));

        result.Should().NotBeNull();
        result!.Percentage.Should().Be(18m);
        result.IsActive.Should().BeFalse();
        // The region and code are what identifies the row — they never change through this call.
        result.FiscalRegion.Should().Be("PT-AC");
        result.Code.Should().Be("NOR");
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
