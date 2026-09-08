using Erp.FiscalPT.Documents;
using FluentAssertions;
using Erp.SeriesRegistry.Domain;

namespace Erp.SeriesRegistry.Tests;

/// <summary>
/// The numbering rules live in the entity, because they are what the certification checks:
/// sequential, without gaps, and only on a series the tax authority knows about.
/// </summary>
public class SeriesTests
{
    private static Series CommunicatedSeries(int initialSequence = 1)
    {
        var series = new Series
        {
            CompanyId = Guid.NewGuid(),
            DocumentType = "FT",
            SeriesCode = "A2026",
            InitialSequence = initialSequence
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        return series;
    }

    [Fact]
    public void TakeNextSequence_produces_consecutive_numbers()
    {
        var series = CommunicatedSeries();

        var numbers = Enumerable.Range(0, 5).Select(_ => series.TakeNextSequence()).ToList();

        numbers.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void TakeNextSequence_honours_the_initial_sequence()
    {
        var series = CommunicatedSeries(initialSequence: 100);

        series.TakeNextSequence().Should().Be(100);
        series.TakeNextSequence().Should().Be(101);
    }

    [Fact]
    public void TakeNextSequence_moves_the_series_to_active()
    {
        var series = CommunicatedSeries();

        series.TakeNextSequence();

        series.Status.Should().Be(SeriesStatus.Active);
    }

    [Fact]
    public void TakeNextSequence_fails_before_the_series_is_communicated()
    {
        var series = new Series { DocumentType = "FT", SeriesCode = "A2026" };

        var act = () => series.TakeNextSequence();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TakeNextSequence_fails_on_a_finalized_series()
    {
        var series = CommunicatedSeries();
        series.Finalize(DateTime.UtcNow);

        var act = () => series.TakeNextSequence();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Communicate_rejects_an_empty_validation_code()
    {
        var series = new Series { DocumentType = "FT", SeriesCode = "A2026" };

        var act = () => series.Communicate("  ", DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Finalize_is_idempotent_and_keeps_the_first_timestamp()
    {
        var series = CommunicatedSeries();
        var first = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        series.Finalize(first);
        series.Finalize(first.AddDays(1));

        series.FinalizedAtUtc.Should().Be(first);
    }
}
