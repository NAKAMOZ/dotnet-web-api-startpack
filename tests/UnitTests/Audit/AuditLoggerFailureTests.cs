using Api.Models.Enums;
using Api.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public LogLevel? Level { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public Exception? Exception { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Message = formatter(state, exception);
            Exception = exception;
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
