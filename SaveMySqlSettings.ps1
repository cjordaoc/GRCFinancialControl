# Save MySQL connection settings to the GRC Financial Control settings database

$settingsDbPath = Join-Path $PSScriptRoot "GRCFinancialControl.Avalonia\bin\Debug\net8.0\settings.db"

# Create settings directory if it doesn't exist
$binDir = Split-Path $settingsDbPath -Parent
if (!(Test-Path $binDir)) {
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null
}

# Load SQLite assembly
Add-Type -Path "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.SQLite.dll" -ErrorAction SilentlyContinue

# Connection settings
$server = "162.241.203.96"
$database = "blac3289_GRCFinancialControl"
$user = "blac3289_GRCFinControl"
$password = "EYbr2@@25#"
$port = "3306"

Write-Host "Saving MySQL connection settings..." -ForegroundColor Cyan

try {
    # Use System.Data.SQLite to interact with the database
    $connectionString = "Data Source=$settingsDbPath;Version=3;"
    
    # Create a simple .NET approach using SQLite
    $code = @"
using System;
using System.Data.SQLite;

public class SettingsSaver
{
    public static void SaveSettings(string dbPath, string server, string database, string user, string password)
    {
        var connectionString = `$"Data Source={dbPath};Version=3;";
        
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
            var settings = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Server", server },
                { "Database", database },
                { "User", user },
                { "Password", password }
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
        
        Console.WriteLine("Settings saved successfully!");
    }
}
"@

    # Try with direct SQL commands if System.Data.SQLite is not available
    # Use sqlite3.exe if available
    $sqlite3Exe = "sqlite3.exe"
    
    # Create and execute SQL
    $sql = @"
CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);
INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('Server', '$server');
INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('Database', '$database');
INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('User', '$user');
INSERT OR REPLACE INTO Settings (Key, Value) VALUES ('Password', '$password');
"@

    # Try to execute via sqlite3
    try {
        $sql | & $sqlite3Exe $settingsDbPath 2>&1 | Out-Null
        Write-Host "✓ MySQL settings saved successfully to: $settingsDbPath" -ForegroundColor Green
        Write-Host ""
        Write-Host "Connection Details:" -ForegroundColor Yellow
        Write-Host "  Server:   $server" -ForegroundColor White
        Write-Host "  Database: $database" -ForegroundColor White
        Write-Host "  User:     $user" -ForegroundColor White
        Write-Host "  Password: ********" -ForegroundColor White
    }
    catch {
        Write-Host "sqlite3.exe not found. Please run the application once to create the database." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Alternatively, use the application's Settings page to configure:" -ForegroundColor Cyan
        Write-Host "  Server:   $server"
        Write-Host "  Port:     $port"
        Write-Host "  Database: $database"
        Write-Host "  User:     $user"
        Write-Host "  Password: $password"
    }
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please use the application's Settings page to configure:" -ForegroundColor Cyan
    Write-Host "  Server:   $server"
    Write-Host "  Port:     $port"
    Write-Host "  Database: $database"
    Write-Host "  User:     $user"
    Write-Host "  Password: $password"
}
