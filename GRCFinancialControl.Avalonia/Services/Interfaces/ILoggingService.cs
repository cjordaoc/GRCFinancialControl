using System;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface ILoggingService
    {
        event Action<string> OnLogMessage;
        void LogInfo(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0);
        void LogWarning(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0);
        void LogError(string message, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0);
    }
}