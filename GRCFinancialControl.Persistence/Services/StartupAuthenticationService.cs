using System;
using System.Linq;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Authentication;
using GRCFinancialControl.Persistence.Authentication;
using GRCFinancialControl.Persistence.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GRCFinancialControl.Persistence.Services
{
    public class StartupAuthenticationService : IStartupAuthenticationService
    {
        private readonly IServiceProvider _services;

        public StartupAuthenticationService(IServiceProvider services)
        {
            _services = services;
        }

        public async Task<string?> AuthenticateAsync(Action<string> setStatusText, Action<bool> setProgressVisible)
        {
            var authService = _services.GetRequiredService<IInteractiveAuthService>();
            var authConfig = _services.GetRequiredService<IAuthConfig>();
            var scopes = authConfig.Scopes?.ToArray() ?? Array.Empty<string>();

            setStatusText("Signing in to Dataverse...");

            try
            {
                var result = await authService.AcquireTokenAsync(scopes).ConfigureAwait(true);
                var user = result.User;

                if (user is not null)
                {
                    var name = !string.IsNullOrWhiteSpace(user.DisplayName)
                        ? user.DisplayName
                        : user.UserPrincipalName ?? "Dataverse user";
                    setStatusText($"Signed in as {name}.");
                }
                else
                {
                    setStatusText("Signed in to Dataverse.");
                }

                await Task.Delay(650).ConfigureAwait(true);
                return null;
            }
            catch (Exception ex)
            {
                var message = AuthenticationMessageFormatter.GetFriendlyMessage(ex);
                setStatusText(message);
                setProgressVisible(false);
                await Task.Delay(2000).ConfigureAwait(true);
                return message;
            }
        }
    }
}