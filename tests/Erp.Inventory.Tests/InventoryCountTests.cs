using Erp.Inventory.Domain;
using FluentAssertions;

namespace Erp.Inventory.Tests;

public class InventoryCountTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static InventoryCountLine Line(string productCode = "ART001", decimal systemQuantity = 10m) => new()
    {
        WarehouseId = WarehouseId,
        ProductCode = productCode,
        ProductDescription = "Artigo de teste",
        SystemQuantity = systemQuantity
    };

    private static InventoryCount Open(bool startAtZero = false, params InventoryCountLine[] lines) =>
        InventoryCount.Open(
            CompanyId,
            WarehouseId,
            InventoryCountScope.Warehouse,
            "INV2026-01",
            new DateOnly(2026, 12, 31),
            lines.Length == 0 ? [Line()] : lines,
            startAtZero,
            "user-1");

    [Fact]
    public void Open_starts_each_line_at_what_the_system_holds()
    {
        var count = Open();

        count.Lines.Single().CountedQuantity.Should().Be(10m);
        count.IsOpen.Should().BeTrue();
    }

    /// <summary>
    /// Zeroing is the same mechanism: a count whose lines start at nothing, so closing it empties
    /// the stock in scope.
    /// </summary>
    [Fact]
    public void Open_starts_each_line_at_zero_when_asked_to()
    {
        var count = Open(startAtZero: true);

        var line = count.Lines.Single();
        line.CountedQuantity.Should().Be(0m);
        line.SystemQuantity.Should().Be(10m);
    }

    [Fact]
    public void Open_attaches_every_line_to_the_count()
    {
        var count = Open(false, Line(), Line("ART002", 5m));

        count.Lines.Should().OnlyContain(line => line.CountId == count.Id);
    }

    [Fact]
    public void Open_refuses_a_warehouse_count_without_a_warehouse()
    {
        var act = () => InventoryCount.Open(
            CompanyId,
            null,
            InventoryCountScope.Warehouse,
            "INV2026-01",
            new DateOnly(2026, 12, 31),
            [Line()],
            false,
            null);

        act.Should().Throw<ArgumentException>().WithMessage("*which*");
    }

    [Fact]
    public void SetCounted_records_what_was_found()
    {
        var count = Open();
        var line = count.Lines.Single();

        count.SetCounted(line.Id, 8m);

        line.CountedQuantity.Should().Be(8m);
    }

    [Fact]
    public void SetCounted_refuses_a_line_from_another_count()
    {
        var count = Open();

        var act = () => count.SetCounted(Guid.NewGuid(), 8m);

        act.Should().Throw<ArgumentException>().WithMessage("*does not belong*");
    }

    [Fact]
    public void Close_returns_the_difference_to_write_to_the_ledger()
    {
        var count = Open();
        var line = count.Lines.Single();
        count.SetCounted(line.Id, 8m);

        var adjustments = count.Close(new Dictionary<Guid, decimal> { [line.Id] = 10m }, "user-2", DateTime.UtcNow);

        adjustments.Should().ContainSingle();
        adjustments[0].Difference.Should().Be(-2m);
        line.AppliedDifference.Should().Be(-2m);
    }

    /// <summary>A line that matches needs no adjustment, and none is produced.</summary>
    [Fact]
    public void Close_leaves_a_matching_line_alone()
    {
        var count = Open();
        var line = count.Lines.Single();

        var adjustments = count.Close(new Dictionary<Guid, decimal> { [line.Id] = 10m }, null, DateTime.UtcNow);

        adjustments.Should().BeEmpty();
        line.AppliedDifference.Should().Be(0m);
    }

    /// <summary>
    /// The whole reason the current balance is passed in: stock that moved while the count was open
    /// must not be undone by it. Counting 8 against a balance that has since become 12 writes -4,
    /// not the -2 the opening picture would have suggested.
    /// </summary>
    [Fact]
    public void Close_measures_against_the_balance_now_and_not_against_the_opening_picture()
    {
        var count = Open();
        var line = count.Lines.Single();
        count.SetCounted(line.Id, 8m);

        var adjustments = count.Close(new Dictionary<Guid, decimal> { [line.Id] = 12m }, null, DateTime.UtcNow);

        adjustments.Single().Difference.Should().Be(-4m);
    }

    /// <summary>A line whose product has no balance at all counts as standing at nothing.</summary>
    [Fact]
    public void Close_treats_a_missing_balance_as_zero()
    {
        var count = Open();
        var line = count.Lines.Single();
        count.SetCounted(line.Id, 3m);

        var adjustments = count.Close(new Dictionary<Guid, decimal>(), null, DateTime.UtcNow);

        adjustments.Single().Difference.Should().Be(3m);
    }

    [Fact]
    public void Close_marks_the_count_closed()
    {
        var count = Open();
        var closedAt = new DateTime(2026, 12, 31, 18, 0, 0, DateTimeKind.Utc);

        count.Close(new Dictionary<Guid, decimal>(), "user-2", closedAt);

        count.IsOpen.Should().BeFalse();
        count.Status.Should().Be(InventoryCountStatus.Closed);
        count.ClosedAtUtc.Should().Be(closedAt);
        count.ClosedByUserId.Should().Be("user-2");
    }

    /// <summary>
    /// A closed count is a record of what was found, so it stays put: reopening it would let the
    /// adjustments already written be quietly contradicted.
    /// </summary>
    [Fact]
    public void A_closed_count_cannot_be_counted_again()
    {
        var count = Open();
        var line = count.Lines.Single();
        count.Close(new Dictionary<Guid, decimal>(), null, DateTime.UtcNow);

        var setCounted = () => count.SetCounted(line.Id, 5m);
        var closeAgain = () => count.Close(new Dictionary<Guid, decimal>(), null, DateTime.UtcNow);

        setCounted.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
        closeAgain.Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    /// <summary>Closing a zeroing count takes everything out, whatever the balance stands at.</summary>
    [Fact]
    public void Closing_a_zeroing_count_empties_the_stock_in_scope()
    {
        var count = Open(startAtZero: true, Line(), Line("ART002", 5m));
        var current = count.Lines.ToDictionary(line => line.Id, line => line.SystemQuantity);

        var adjustments = count.Close(current, null, DateTime.UtcNow);

        adjustments.Should().HaveCount(2);
        adjustments.Select(x => x.Difference).Should().BeEquivalentTo([-10m, -5m]);
    }
}
