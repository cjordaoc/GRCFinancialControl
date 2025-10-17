using System;
using System.Threading.Tasks;

namespace GRCFinancialControl.Persistence.Services.Interfaces
{
    public interface IStartupAuthenticationService
    {
        Task<string?> AuthenticateAsync(Action<string> setStatusText, Action<bool> setProgressVisible);
    }
}