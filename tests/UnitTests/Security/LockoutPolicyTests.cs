using Api.Configuration;
using Api.Models;
using Api.Services.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace UnitTests.Security;

public class LockoutPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RegisterFailure_IncrementsTheConsecutiveFailureCount()
    {
        var (policy, _, user) = Create();

        var transitioned = policy.RegisterFailure(user);

        Assert.False(transitioned);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void RegisterFailure_AtTheThreshold_LocksForTheConfiguredDuration()
    {
        var (policy, _, user) = Create(failedLoginCount: 4);

        var transitioned = policy.RegisterFailure(user);

        Assert.True(transitioned);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(15), user.LockoutEndsAt);
        Assert.True(policy.IsLockedOut(user));
    }

    [Fact]
    public void RegisterFailure_WhileAlreadyLocked_DoesNotExtendOrReauditTheLock()
    {
        var existingEnd = Now.AddMinutes(8);
        var (policy, _, user) = Create(failedLoginCount: 5, lockoutEndsAt: existingEnd);

        var transitioned = policy.RegisterFailure(user);

        Assert.False(transitioned);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.Equal(existingEnd, user.LockoutEndsAt);
    }

    [Fact]
    public void RegisterFailure_AfterTheLockExpires_GrantsAFreshAllowance()
    {
        var (policy, _, user) = Create(
            failedLoginCount: 5,
            lockoutEndsAt: Now.AddTicks(-1));

        var transitioned = policy.RegisterFailure(user);

        Assert.False(transitioned);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void IsLockedOut_AtTheExactExpiryBoundary_ReturnsFalse()
    {
        var (policy, _, user) = Create(failedLoginCount: 5, lockoutEndsAt: Now);

        Assert.False(policy.IsLockedOut(user));
    }

    [Fact]
    public void RegisterSuccess_ClearsBothFailureFields()
    {
        var (policy, _, user) = Create(
            failedLoginCount: 4,
            lockoutEndsAt: Now.AddMinutes(-1));

        policy.RegisterSuccess(user);

        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void DisabledPolicy_DoesNotMutateTheUser()
    {
        var (policy, _, user) = Create(
            enabled: false,
            failedLoginCount: 4,
            lockoutEndsAt: Now.AddMinutes(2));

        Assert.False(policy.IsLockedOut(user));
        Assert.False(policy.RegisterFailure(user));
        Assert.Equal(4, user.FailedLoginCount);
        Assert.Equal(Now.AddMinutes(2), user.LockoutEndsAt);
    }

    private static (LockoutPolicy Policy, FakeTimeProvider Time, User User) Create(
        bool enabled = true,
        int failedLoginCount = 0,
        DateTimeOffset? lockoutEndsAt = null)
    {
        var time = new FakeTimeProvider(Now);
        var policy = new LockoutPolicy(
            Options.Create(new LockoutOptions
            {
                Enabled = enabled,
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15),
            }),
            time);

        var user = new User
        {
            Email = "lockout@example.com",
            FailedLoginCount = failedLoginCount,
            LockoutEndsAt = lockoutEndsAt,
        };

        return (policy, time, user);
    }
}
