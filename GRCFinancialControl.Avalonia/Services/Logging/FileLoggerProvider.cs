using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Avalonia.Services.Logging
{
    /// <summary>
    /// Minimal file logger for capturing application diagnostics to disk.
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly object _sync = new();
        private IExternalScopeProvider? _scopeProvider;

        public FileLoggerProvider(string logDirectory, string? fileName = null, LogLevel minLevel = LogLevel.Information)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

            var timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
            var normalizedFileName = string.IsNullOrWhiteSpace(fileName)
                ? string.Concat(timestamp, ".log")
                : fileName;

            Directory.CreateDirectory(logDirectory);
            _filePath = Path.Combine(logDirectory, normalizedFileName);
            _minLevel = minLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
            return new FileLogger(categoryName, _filePath, _minLevel, _sync, () => _scopeProvider);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public void Dispose()
        {
        }

        private sealed class FileLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly string _filePath;
            private readonly LogLevel _minLevel;
            private readonly object _sync;
            private readonly Func<IExternalScopeProvider?> _scopeProviderAccessor;

            public FileLogger(string categoryName, string filePath, LogLevel minLevel, object sync, Func<IExternalScopeProvider?> scopeProviderAccessor)
            {
                _categoryName = categoryName;
                _filePath = filePath;
                _minLevel = minLevel;
                _sync = sync;
                _scopeProviderAccessor = scopeProviderAccessor;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
            {
                var scopeProvider = _scopeProviderAccessor();
                return scopeProvider?.Push(state) ?? NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel >= _minLevel;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                if (formatter == null)
                {
                    throw new ArgumentNullException(nameof(formatter));
                }

                var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
                var message = formatter(state, exception);

                var builder = new StringBuilder();
                builder.Append('[').Append(timestamp).Append(']');
                builder.Append(' ').Append('[').Append(logLevel).Append(']');
                builder.Append(' ').Append(_categoryName).Append(" :: ");
                builder.AppendLine(message);

                var scopeProvider = _scopeProviderAccessor();
                if (scopeProvider != null)
                {
                    scopeProvider.ForEachScope((scope, sb) => sb.AppendLine($"Scope: {scope}"), builder);
                }

                if (exception != null)
                {
                    builder.AppendLine(exception.ToString());
                }

                var payload = builder.ToString();

                lock (_sync)
                {
                    File.AppendAllText(_filePath, payload);
                }
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
