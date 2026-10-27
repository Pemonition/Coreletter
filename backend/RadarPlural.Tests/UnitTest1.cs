using RadarPlural.Api;

namespace RadarPlural.Tests;


public class DomainTests
{
    [Theory]
    [InlineData("https://example.org/news", true)]
    [InlineData("http://example.org", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("https://user:secret@example.org", false)]
    [InlineData("/relative", false)]
    public void SharedLinksOnlyAllowAbsoluteHttpWithoutCredentials(string value, bool expected) => Assert.Equal(expected, Rules.ValidUrl(value));

    [Fact]
    public void RepeatingRequestDoesNotConsumeQuotaTwice()
    {
        var budget = new UsageBudget(1, 2m);
        var id = Guid.NewGuid();
        Assert.True(budget.TryReserve(id, 1m));
        Assert.True(budget.TryReserve(id, 1m));
        Assert.Equal((1, 1m), budget.Snapshot());
        Assert.False(budget.TryReserve(Guid.NewGuid(), 0m));
    }
    [Fact]
    public void BudgetBoundaryIsInclusiveAndRejectsOverspend()
    {
        var budget = new UsageBudget(10, 1m);
        Assert.True(budget.TryReserve(Guid.NewGuid(), 1m));
        Assert.False(budget.TryReserve(Guid.NewGuid(), 0.01m));
        Assert.Equal((1, 1m), budget.Snapshot());
    }
    [Fact]
    public void NegativeCostCannotCreditBudget()
    {
        var budget = new UsageBudget(10, 1m);
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.TryReserve(Guid.NewGuid(), -1m));
        Assert.Equal((0, 0m), budget.Snapshot());
    }
    [Fact]
    public void ParallelReservationsCannotExceedQuotaOrBudget()
    {
        var budget = new UsageBudget(7, 1m);
        Parallel.For(0, 100, _ => budget.TryReserve(Guid.NewGuid(), 0.2m));
        Assert.Equal((5, 1m), budget.Snapshot());
    }
    [Fact]
    public void ConcurrentRetriesReserveExactlyOnce()
    {
        var budget = new UsageBudget(100, 100m);
        var id = Guid.NewGuid();
        Parallel.For(0, 100, _ => Assert.True(budget.TryReserve(id, 1m)));
        Assert.Equal((1, 1m), budget.Snapshot());
    }
}

public class FinancialTests
{
    [Fact]
    public void PlanReservesRequiredContribution()
    {
        Assert.Equal(6.96m, RadarPlural.Api.FinancialRules.AiBudget(29.90m, 5m, 0.6m));
    }
    [Fact]
    public void BudgetRoundsDownToAvoidOverspend()
    {
        Assert.Equal(0.33m, RadarPlural.Api.FinancialRules.AiBudget(1m, 0m, 0.666m));
    }
    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, 2)]
    [InlineData(1, 0, -1)]
    public void InvalidPlanRejected(decimal price, decimal other, decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RadarPlural.Api.FinancialRules.AiBudget(price, other, rate));
    }
    [Fact]
    public void UnfundedPlanRejected()
    {
        Assert.Throws<ArgumentException>(() => RadarPlural.Api.FinancialRules.AiBudget(1m, 2m, 0m));
    }
    [Fact]
    public void ContributionIsDistinctFromResult()
    {
        Assert.Equal((209m, 109m), RadarPlural.Api.FinancialRules.Period(299m, 50m, 40m, 100m));
    }
    [Fact]
    public void LossMustRemainVisible()
    {
        Assert.Equal((-2m, -7m), RadarPlural.Api.FinancialRules.Period(3m, 2m, 3m, 5m));
    }
}


public class AuthenticationPrimitiveTests
{
    [Fact]
    public void PasswordHashesAreSaltedAndRejectWrongPassword()
    {
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<RadarPlural.Api.Member>();
        var member = new RadarPlural.Api.Member();
        const string password = "fixture-password-for-tests";
        var first = hasher.HashPassword(member, password);
        var second = hasher.HashPassword(member, password);
        Assert.NotEqual(first, second);
        Assert.NotEqual(password, first);
        Assert.NotEqual(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(member, first, password));
        Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(member, first, "wrong-password"));
    }
}
