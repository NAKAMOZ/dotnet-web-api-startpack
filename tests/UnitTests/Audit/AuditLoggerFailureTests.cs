using Api.Models.Enums;
using Api.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.TestSupport;

namespace UnitTests.Audit;

public sealed class AuditLoggerFailureTests
{
    [Fact]
    public async Task WriteFailure_EmitsCriticalFallbackWithoutMetadataAndDoesNotLieAboutRollback()
    {
        var logger = new CapturingLogger<AuditLogger>();
        var auditLogger = new AuditLogger(
            new ThrowingScopeFactory(),
            new HttpContextAccessor(),
            TimeProvider.System,
            logger);

        await auditLogger.LogAsync(
            AuditEventType.LoginFailed,
            Guid.CreateVersion7(),
            new { AccessToken = "must-never-reach-fallback" },
            TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Critical, logger.Level);
        Assert.Contains(nameof(AuditEventType.LoginFailed), logger.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-never-reach-fallback", logger.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(logger.Exception);
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("audit database unavailable");
    }

}
