using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace GRCFinancialControl.Persistence.Logging
{
    /// <summary>
    /// Logger helpers that prepend caller context (file, line, member) to log messages.
    /// </summary>
    public static class LoggerExtensions
    {
        public static void LogErrorWithContext(
            this ILogger logger,
            Exception exception,
            string messageTemplate,
            params object[] args)
        {
            LogWithContext(logger, LogLevel.Error, exception, messageTemplate, args);
        }

        public static void LogWarningWithContext(
            this ILogger logger,
            Exception exception,
            string messageTemplate,
            params object[] args)
        {
            LogWithContext(logger, LogLevel.Warning, exception, messageTemplate, args);
        }

        private static void LogWithContext(
            ILogger logger,
            LogLevel level,
            Exception exception,
            string messageTemplate,
            object[] args,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var fileName = string.IsNullOrWhiteSpace(filePath)
                ? "UnknownFile"
                : Path.GetFileName(filePath);

            var contextArgs = new object[] { fileName, lineNumber, memberName };
            var mergedArgs = contextArgs.Concat(args ?? Array.Empty<object>()).ToArray();

            var template = "[{File}:{Line}:{Member}] " + messageTemplate;

            if (level == LogLevel.Error)
            {
                logger.LogError(exception, template, mergedArgs);
                return;
            }

            if (level == LogLevel.Warning)
            {
                logger.LogWarning(exception, template, mergedArgs);
                return;
            }

            logger.Log(level, new EventId(), mergedArgs, exception, (s, e) => template);
        }
    }
}
