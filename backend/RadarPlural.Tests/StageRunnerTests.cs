using RadarPlural.Pipeline;

namespace RadarPlural.Tests;

// Executor de etapas (docs/decisoes/0004): regras puras, sem banco.
public class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void BackoffDoublesEachAttempt(int attempt, int minutes) => Assert.Equal(TimeSpan.FromMinutes(minutes), RetryPolicy.Backoff(attempt));

    [Fact]
    public void BackoffIsCappedAtOneHour() => Assert.Equal(TimeSpan.FromHours(1), RetryPolicy.Backoff(20));

    [Theory]
    [InlineData(1, 3, false)]
    [InlineData(2, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    public void ExhaustedAfterMaxAttempts(int attempt, int max, bool expected) => Assert.Equal(expected, RetryPolicy.Exhausted(attempt, max));
}

public class StageRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeQueue(params ClaimedStory[] stories) : IStoryQueue
    {
        public CompletionResult CompletionAnswer { get; set; } = CompletionResult.Completed;
        public List<(Guid Id, string Error, DateTimeOffset? RetryAt)> Failures { get; } = [];
        public List<Guid> Completed { get; } = [];
        public int ClaimMax { get; private set; }

        public Task<IReadOnlyList<ClaimedStory>> ClaimAsync(StageDefinition stage, string workerId, int max, DateTimeOffset now, CancellationToken ct)
        {
            ClaimMax = max;
            return Task.FromResult<IReadOnlyList<ClaimedStory>>(stories.Take(max).ToList());
        }

        public Task<CompletionResult> CompleteAsync(StageDefinition stage, ClaimedStory story, string workerId, DateTimeOffset now, CancellationToken ct)
        {
            if (CompletionAnswer == CompletionResult.Completed) Completed.Add(story.StoryId);
            return Task.FromResult(CompletionAnswer);
        }

        public Task<FailureResult> FailAsync(StageDefinition stage, ClaimedStory story, string workerId, string error, DateTimeOffset? retryAt, DateTimeOffset now, CancellationToken ct)
        {
            Failures.Add((story.StoryId, error, retryAt));
            return Task.FromResult(retryAt is null ? FailureResult.GaveUp : FailureResult.Retrying);
        }
    }

    private sealed class Handler(Func<ClaimedStory, StageOutcome> act) : IStageHandler
    {
        public StageDefinition Stage => Stages.Auditoria;
        public Task<StageOutcome> HandleAsync(ClaimedStory story, CancellationToken ct) => Task.FromResult(act(story));
    }

    private static ClaimedStory Story(int attempt = 1) => new(Guid.NewGuid(), attempt, 1);

    private static StageRunner Runner(IStoryQueue q, Func<ClaimedStory, StageOutcome> act) => new(q, new Handler(act), "worker-a", new FixedClock(Now));

    [Fact]
    public async Task DoneCompletesTheStory()
    {
        var s = Story();
        var q = new FakeQueue(s);
        var r = await Runner(q, _ => new StageOutcome.Done()).RunOnceAsync(10, default);
        Assert.Equal([s.StoryId], q.Completed);
        Assert.Equal(new RunSummary(1, 1, 0, 0, 0, 0), r);
    }

    [Fact]
    public async Task RetryBeforeLimitSchedulesWithBackoff()
    {
        var q = new FakeQueue(Story(attempt: 2));
        var r = await Runner(q, _ => new StageOutcome.Retry("provedor fora do ar")).RunOnceAsync(10, default);
        var f = Assert.Single(q.Failures);
        Assert.Equal(Now + TimeSpan.FromMinutes(2), f.RetryAt);
        Assert.Equal(1, r.Retrying);
    }

    [Fact]
    public async Task RetryOnLastAttemptGivesUp()
    {
        var q = new FakeQueue(Story(attempt: Stages.Auditoria.MaxAttempts));
        var r = await Runner(q, _ => new StageOutcome.Retry("x")).RunOnceAsync(10, default);
        Assert.Null(Assert.Single(q.Failures).RetryAt);
        Assert.Equal(1, r.GaveUp);
    }

    [Fact]
    public async Task UnexpectedExceptionIsTreatedAsRetryAndDoesNotStopTheBatch()
    {
        var bad = Story();
        var good = Story();
        var q = new FakeQueue(bad, good);
        var r = await Runner(q, s => s == bad ? throw new InvalidOperationException("bug") : new StageOutcome.Done()).RunOnceAsync(10, default);
        Assert.Contains("bug", Assert.Single(q.Failures).Error);
        Assert.Equal([good.StoryId], q.Completed);
        Assert.Equal(new RunSummary(2, 1, 1, 0, 0, 0), r);
    }

    [Fact]
    public async Task LongErrorMessagesAreTruncatedForStorage()
    {
        var q = new FakeQueue(Story());
        await Runner(q, _ => new StageOutcome.Retry(new string('x', 2000))).RunOnceAsync(10, default);
        Assert.True(q.Failures[0].Error.Length <= 500);
    }

    [Theory]
    [InlineData(CompletionResult.Stale)]
    [InlineData(CompletionResult.LeaseLost)]
    public async Task StaleOrLostCompletionIsCountedNotFailed(CompletionResult answer)
    {
        var q = new FakeQueue(Story()) { CompletionAnswer = answer };
        var r = await Runner(q, _ => new StageOutcome.Done()).RunOnceAsync(10, default);
        Assert.Empty(q.Failures);
        Assert.Equal(answer == CompletionResult.Stale ? 1 : 0, r.Stale);
        Assert.Equal(answer == CompletionResult.LeaseLost ? 1 : 0, r.LeaseLost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task BatchSizeIsValidated(int size) =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Runner(new FakeQueue(), _ => new StageOutcome.Done()).RunOnceAsync(size, default));

    [Fact]
    public async Task CancellationBeforeHandlingStopsWithoutCompleting()
    {
        var q = new FakeQueue(Story(), Story());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner(q, _ => new StageOutcome.Done()).RunOnceAsync(10, cts.Token));
        Assert.Empty(q.Completed);
    }
}
