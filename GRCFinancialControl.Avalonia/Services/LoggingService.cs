using System;
using System.IO;
using System.Runtime.CompilerServices;
using GRCFinancialControl.Avalonia.Services.Interfaces;

namespace GRCFinancialControl.Avalonia.Services
{
    public class LoggingService : ILoggingService
    {
        public event Action<string> OnLogMessage;

        private int _messageId = 0;

        public void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log("INFO", message, memberName, sourceFilePath, sourceLineNumber);
        }

        public void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log("WARNING", message, memberName, sourceFilePath, sourceLineNumber);
        }

        public void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            Log("ERROR", message, memberName, sourceFilePath, sourceLineNumber);
        }

        private void Log(string level, string message, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            var formattedMessage = $"[{level}][{_messageId++}] {Path.GetFileName(sourceFilePath)}:{sourceLineNumber} ({memberName}) - {message}";
            Console.WriteLine(formattedMessage);
            OnLogMessage?.Invoke(formattedMessage);
        }
    }
}