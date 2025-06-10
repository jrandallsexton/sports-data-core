using Microsoft.Extensions.Logging;

namespace SportsData.Tests.Shared;

public enum LoggerTypes
{
    Null,
    List
}

public class NullScope : IDisposable
{
    public static NullScope Instance { get; } = new NullScope();

    private NullScope() { }

    public void Dispose()
    { }
}

public class ListLogger : ILogger
{
    public IList<string> Logs = new List<string>();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception, string> formatter)
    {
        if (exception == null) return;
        var message = formatter(state, exception);
        this.Logs.Add($"{LogLevelLabel(logLevel)} {message}");
    }

    private static string LogLevelLabel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "[TRACE]",
            LogLevel.Debug => "[DEBUG]",
            LogLevel.Information => "[INFORMATION]",
            LogLevel.Warning => "[WARNING]",
            LogLevel.Error => "[ERROR]",
            LogLevel.Critical => "[CRITICAL]",
            _ => string.Empty,
        };
    }
}