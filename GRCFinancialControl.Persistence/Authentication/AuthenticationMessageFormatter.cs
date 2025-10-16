using System;
using Microsoft.Identity.Client;

namespace GRCFinancialControl.Persistence.Authentication;

public static class AuthenticationMessageFormatter
{
    public static string GetFriendlyMessage(Exception exception)
    {
        if (exception is null)
        {
            return "An unexpected authentication error occurred.";
        }

        var msalException = FindMsalException(exception);
        if (msalException is MsalClientException clientException &&
            string.Equals(clientException.ErrorCode, "authentication_canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "Sign-in was canceled.";
        }

        if (msalException is MsalServiceException serviceException)
        {
            return serviceException.ErrorCode switch
            {
                "AADSTS65001" => "Administrator consent is required for this application to access Dataverse. Ask your administrator to grant the Dynamics CRM user_impersonation permission.",
                "AADSTS50079" or "AADSTS50076" => "Additional authentication is required. Complete multi-factor authentication in the sign-in window.",
                "invalid_grant" when ContainsIgnoreCase(serviceException.Message, "AADSTS50020")
                    => "Your account is blocked from signing in. Contact your administrator to lift access restrictions.",
                _ => serviceException.Message ?? "An unexpected authentication error occurred."
            };
        }

        if (exception is InvalidOperationException invalidOperation &&
            invalidOperation.Message.Contains("Access to Dataverse was denied", StringComparison.OrdinalIgnoreCase))
        {
            return invalidOperation.Message;
        }

        var message = exception.Message ?? "An unexpected authentication error occurred.";
        if (ContainsIgnoreCase(message, "license"))
        {
            return "Access to Dataverse requires an active Dynamics 365 license. Contact your administrator.";
        }

        if (ContainsIgnoreCase(message, "privilege") ||
            ContainsIgnoreCase(message, "security role") ||
            ContainsIgnoreCase(message, "permission"))
        {
            return "Access to Dataverse was denied. Verify your security role assignments.";
        }

        return message;
    }

    private static Exception? FindMsalException(Exception exception)
    {
        if (exception is MsalException)
        {
            return exception;
        }

        return exception.InnerException is null ? null : FindMsalException(exception.InnerException);
    }

    private static bool ContainsIgnoreCase(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
