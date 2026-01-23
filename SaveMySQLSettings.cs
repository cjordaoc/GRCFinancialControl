using System;
using System.Data.SQLite;
using System.IO;

class SaveMySQLSettings
{
    static void Main()
    {
        var settingsDbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "settings.db");

        Console.WriteLine($"Settings database path: {settingsDbPath}");

        try
        {
            var connectionString = $"Data Source={settingsDbPath};Version=3;";
            
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                
                // Create table if it doesn't exist
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Settings (
                            Key TEXT PRIMARY KEY,
                            Value TEXT NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }
                
                // Insert or update settings
                var settings = new (string Key, string Value)[]
                {
                    ("Server", "162.241.203.96"),
                    ("Database", "blac3289_GRCFinancialControl"),
                    ("User", "blac3289_GRCFinControl"),
                    ("Password", "EYbr2@@25#")
                };
                
                foreach (var setting in settings)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO Settings (Key, Value)
                            VALUES (@key, @value)";
                        cmd.Parameters.AddWithValue("@key", setting.Key);
                        cmd.Parameters.AddWithValue("@value", setting.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ MySQL settings saved successfully!");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Connection Details:");
            Console.WriteLine("  Server:   162.241.203.96");
            Console.WriteLine("  Database: blac3289_GRCFinancialControl");
            Console.WriteLine("  User:     blac3289_GRCFinControl");
            Console.WriteLine("  Password: ********");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Please configure the settings manually in the application:");
            Console.WriteLine("  Go to Settings → Database Connection");
            Console.WriteLine("  Server:   162.241.203.96");
            Console.WriteLine("  Database: blac3289_GRCFinancialControl");
            Console.WriteLine("  User:     blac3289_GRCFinControl");
            Console.WriteLine("  Password: EYbr2@@25#");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
