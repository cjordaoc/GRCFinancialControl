using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GRCFinancialControl.Avalonia.Services
{
    /// <summary>
    /// Provides simple logging with caller info and event broadcasting.
    /// </summary>
    public sealed class LoggingService
    {
        private const string InfoLevel = "INFO";
        private const string WarningLevel = "WARNING";
        private const string ErrorLevel = "ERROR";
        private const string UnknownMemberPlaceholder = "UnknownMember";
        private const string UnknownFilePlaceholder = "UnknownFile";

        public event Action<string> OnLogMessage = delegate { };

        private int _messageId;

        public void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(InfoLevel, message, memberName, sourceFilePath, sourceLineNumber);
        }

        public void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(WarningLevel, message, memberName, sourceFilePath, sourceLineNumber);
        }

        public void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log(ErrorLevel, message, memberName, sourceFilePath, sourceLineNumber);
        }

        private void Log(string level, string message, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(level);
            ArgumentNullException.ThrowIfNull(message);

            var safeMemberName = string.IsNullOrWhiteSpace(memberName) ? UnknownMemberPlaceholder : memberName;
            var safeFileName = string.IsNullOrWhiteSpace(sourceFilePath) ? UnknownFilePlaceholder : Path.GetFileName(sourceFilePath);
            var messageId = Interlocked.Increment(ref _messageId) - 1;

            var formattedMessage = $"[{level}][{messageId}] {safeFileName}:{sourceLineNumber} ({safeMemberName}) - {message}";
            Console.WriteLine(formattedMessage);
            OnLogMessage?.Invoke(formattedMessage);
        }
    }
}