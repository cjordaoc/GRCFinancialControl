using System;
using System.Runtime.CompilerServices;

namespace GRCFinancialControl.Avalonia.Services.Interfaces
{
    public interface ILoggingService
    {
        event Action<string> OnLogMessage;
        void LogInfo(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);
        void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);
        void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);
    }
}
