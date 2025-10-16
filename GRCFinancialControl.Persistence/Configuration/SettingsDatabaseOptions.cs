namespace GRCFinancialControl.Persistence.Configuration;

public static class SettingsDatabaseOptions
{
    public const string DatabaseFileName = "settings.db";

    public static string BuildConnectionString()
    {
        return $"Data Source={DatabaseFileName}";
    }
}
