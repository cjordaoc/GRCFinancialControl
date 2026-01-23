using System;
using System.Data.SQLite;
using System.IO;

var settingsDbPath = Path.Combine(
    Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
    "..", "..", "..", "GRCFinancialControl.Avalonia", "bin", "Debug", "net8.0", "settings.db");

settingsDbPath = Path.GetFullPath(settingsDbPath);

Console.WriteLine($"Settings database: {settingsDbPath}");

if (!File.Exists(settingsDbPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("settings.db not found!");
    Console.ResetColor();
    return 1;
}

try
{
    var connectionString = $"Data Source={settingsDbPath};Version=3;";
    
    using var connection = new SQLiteConnection(connectionString);
    connection.Open();
    
    var settings = new (string Key, string Value)[]
    {
        ("Server", "162.241.203.96"),
        ("Database", "blac3289_GRCFinancialControl"),
        ("User", "blac3289_GRCFinControl"),
        ("Password", "EYbr2@@25#")
    };
    
    foreach (var (key, value) in settings)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@key, @value)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }
    
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n✓ MySQL connection settings saved successfully!\n");
    Console.ResetColor();
    Console.WriteLine("Connection Details:");
    Console.WriteLine("  Server:   162.241.203.96");
    Console.WriteLine("  Port:     3306");
    Console.WriteLine("  Database: blac3289_GRCFinancialControl");
    Console.WriteLine("  User:     blac3289_GRCFinControl");
    Console.WriteLine("  Password: ********");
    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nError: {ex.Message}");
    Console.ResetColor();
    return 1;
}
