using Erp.FiscalPT.Documents;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

/// <summary>
/// The rules a series obeys, on their own. What is not tested here is the row lock that makes the
/// numbering safe under concurrency — that belongs to storage, and this only says what happens once
/// the row is held.
/// </summary>
public class SeriesStateTests
{
    private static SeriesState Communicated(int initialSequence = 1) =>
        SeriesState.New(initialSequence).Communicate("AAJFF7RTGY");

    [Fact]
    public void A_new_series_cannot_issue()
    {
        var state = SeriesState.New();

        state.Status.Should().Be(SeriesStatus.Created);
        state.CanIssue.Should().BeFalse();
    }

    [Fact]
    public void New_refuses_a_sequence_below_one()
    {
        var act = () => SeriesState.New(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Communicating_records_the_validation_code_and_allows_issuing()
    {
        var state = Communicated();

        state.Status.Should().Be(SeriesStatus.Communicated);
        state.ValidationCode.Should().Be("AAJFF7RTGY");
        state.CanIssue.Should().BeTrue();
    }

    /// <summary>
    /// Without a validation code the tax authority has not accepted the series, and documents
    /// issued on it would carry an ATCUD that means nothing.
    /// </summary>
    [Fact]
    public void A_series_without_a_validation_code_cannot_issue()
    {
        var state = SeriesState.New() with { Status = SeriesStatus.Active };

        state.CanIssue.Should().BeFalse();
    }

    [Fact]
    public void Taking_a_number_advances_the_sequence_and_activates_the_series()
    {
        var (state, sequence) = Communicated().TakeNextSequence();

        sequence.Should().Be(1);
        state.CurrentSequence.Should().Be(1);
        state.Status.Should().Be(SeriesStatus.Active);
    }

    [Fact]
    public void Numbers_come_out_one_after_another()
    {
        var state = Communicated();
        var taken = new List<int>();

        for (var i = 0; i < 4; i++)
        {
            (state, var sequence) = state.TakeNextSequence();
            taken.Add(sequence);
        }

        taken.Should().Equal(1, 2, 3, 4);
    }

    /// <summary>
    /// A series continuing from another system starts at the number that was declared, rather than
    /// counting up to it.
    /// </summary>
    [Fact]
    public void A_series_that_starts_above_one_issues_that_number_first()
    {
        var (state, sequence) = Communicated(initialSequence: 500).TakeNextSequence();

        sequence.Should().Be(500);
        state.CurrentSequence.Should().Be(500);

        (_, var next) = state.TakeNextSequence();
        next.Should().Be(501);
    }

    [Fact]
    public void A_series_that_cannot_issue_refuses_to_hand_out_a_number()
    {
        var act = () => SeriesState.New().TakeNextSequence();

        act.Should().Throw<InvalidOperationException>().WithMessage("*not available*");
    }

    [Fact]
    public void A_finalized_series_cannot_issue_or_be_communicated_again()
    {
        var state = Communicated().Finalize();

        state.CanIssue.Should().BeFalse();
        state.IsFinalized.Should().BeTrue();

        var communicate = () => state.Communicate("OUTRO");
        var take = () => state.TakeNextSequence();

        communicate.Should().Throw<InvalidOperationException>().WithMessage("*finalized*");
        take.Should().Throw<InvalidOperationException>().WithMessage("*not available*");
    }

    [Fact]
    public void Finalizing_twice_changes_nothing()
    {
        var once = Communicated().Finalize();

        once.Finalize().Should().Be(once);
    }

    /// <summary>Every operation returns the next state, so nothing is changed under the caller.</summary>
    [Fact]
    public void The_state_is_never_mutated_in_place()
    {
        var original = Communicated();

        original.TakeNextSequence();
        original.Finalize();

        original.CurrentSequence.Should().Be(0);
        original.Status.Should().Be(SeriesStatus.Communicated);
    }
}
