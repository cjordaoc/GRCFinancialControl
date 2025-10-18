using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GRCFinancialControl.Core.Configuration;
using GRCFinancialControl.Persistence.Services.Interfaces;

namespace GRCFinancialControl.Persistence.Services;

public sealed class ConnectionPackageService : IConnectionPackageService
{
    private const int CurrentVersion = 1;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int IterationCount = 100_000;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ISettingsService _settingsService;

    public ConnectionPackageService(ISettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task ExportAsync(string filePath, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A destination file path is required.", nameof(filePath));
        }

        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("A passphrase is required to protect the package.", nameof(passphrase));
        }

        var settings = await _settingsService.GetAllAsync().ConfigureAwait(false);
        var payload = BuildPayload(settings);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var keyDerivation = new Rfc2898DeriveBytes(passphrase, salt, IterationCount, HashAlgorithmName.SHA256);
        using var aes = Aes.Create();
        aes.Key = keyDerivation.GetBytes(KeySize);
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(payloadJson);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var envelope = new ConnectionPackageEnvelope
        {
            Version = CurrentVersion,
            Algorithm = "AES-256-CBC",
            Iterations = IterationCount,
            Salt = Convert.ToBase64String(salt),
            Iv = Convert.ToBase64String(aes.IV),
            CipherText = Convert.ToBase64String(cipherBytes),
            CreatedUtc = payload.CreatedUtc
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        var envelopeJson = JsonSerializer.Serialize(envelope, SerializerOptions);
        await File.WriteAllTextAsync(filePath, envelopeJson, Encoding.UTF8).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> ImportAsync(string filePath, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A package file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The specified package file does not exist.", filePath);
        }

        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("A passphrase is required to read the package.", nameof(passphrase));
        }

        var envelopeJson = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
        var envelope = JsonSerializer.Deserialize<ConnectionPackageEnvelope>(envelopeJson, SerializerOptions)
            ?? throw new InvalidOperationException("The package file is corrupted or unsupported.");

        if (envelope.Version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported package version: {envelope.Version}.");
        }

        var salt = Convert.FromBase64String(envelope.Salt);
        var iv = Convert.FromBase64String(envelope.Iv);
        var cipher = Convert.FromBase64String(envelope.CipherText);

        try
        {
            using var keyDerivation = new Rfc2898DeriveBytes(passphrase, salt, envelope.Iterations, HashAlgorithmName.SHA256);
            using var aes = Aes.Create();
            aes.Key = keyDerivation.GetBytes(KeySize);
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            var payloadJson = Encoding.UTF8.GetString(plainBytes);
            var payload = JsonSerializer.Deserialize<ConnectionPackagePayload>(payloadJson, SerializerOptions)
                ?? throw new InvalidOperationException("The package payload is invalid.");

            if (payload.Version != CurrentVersion)
            {
                throw new InvalidOperationException($"Unsupported payload version: {payload.Version}.");
            }

            var settings = RequiredKeys
                .ToDictionary(key => key, key => payload.Settings.TryGetValue(key, out var value) ? value : string.Empty);

            ValidateSettings(settings);
            return settings;
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("The passphrase is incorrect or the package is corrupted.");
        }
    }

    private static ConnectionPackagePayload BuildPayload(Dictionary<string, string> settings)
    {
        if (settings is null)
        {
            throw new InvalidOperationException("Connection settings are not available.");
        }

        var payloadSettings = RequiredKeys.ToDictionary(key => key, key => settings.TryGetValue(key, out var value) ? value : string.Empty);
        ValidateSettings(payloadSettings);

        return new ConnectionPackagePayload
        {
            Version = CurrentVersion,
            CreatedUtc = DateTimeOffset.UtcNow,
            Settings = payloadSettings
        };
    }

    private static void ValidateSettings(IReadOnlyDictionary<string, string> settings)
    {
        var missing = RequiredKeys
            .Where(key => !settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Connection settings are incomplete. Missing values: {string.Join(", ", missing)}.");
        }
    }

    private static readonly string[] RequiredKeys =
    {
        SettingKeys.Server,
        SettingKeys.Database,
        SettingKeys.User,
        SettingKeys.Password
    };

    private sealed class ConnectionPackageEnvelope
    {
        public int Version { get; set; }
        public string Algorithm { get; set; } = string.Empty;
        public int Iterations { get; set; }
        public string Salt { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string CipherText { get; set; } = string.Empty;
        public DateTimeOffset CreatedUtc { get; set; }
    }

    private sealed class ConnectionPackagePayload
    {
        public int Version { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
