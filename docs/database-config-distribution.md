# Sharing Database Connection Configuration Between GRC Financial Control and Invoice Planner

This note compares two distribution models so the Invoice Planner desktop client can reuse the database configuration defined in GRC Financial Control without manual recopying. Both options keep sensitive connection strings encrypted at rest and minimise operator effort.

## Option A — Central Configuration Service (Recommended)

Use a managed configuration store that both desktop applications can read at startup. Azure App Configuration paired with Azure Key Vault keeps application settings and credentials in one place and supports automatic refresh hooks for .NET clients.

**How it works**
1. Provision an Azure App Configuration instance and register the existing MySQL connection settings and any environment flags as key–value pairs.
2. Store the actual connection string inside Azure Key Vault and reference it from App Configuration using the `@Microsoft.KeyVault(...)` syntax. This keeps secrets out of the desktop packages while preserving a single lookup location.[^1]
3. Add the `Microsoft.Extensions.Configuration.AzureAppConfiguration` package to both Avalonia solutions and configure a refresh pipeline. The built-in configuration refresher invalidates cached values when the store version changes, so Invoice Planner receives the update the next time it polls or on the next launch.[^1]
4. Use Azure AD service principals (or managed identities when available) so the applications authenticate without embedding raw credentials.

**Why this is simplest overall**
- No operators distributing files.
- Secrets never touch disk unencrypted.
- Configuration drift cannot occur because the store is authoritative.
- Fits naturally into the existing .NET `IConfiguration` bootstrap.

## Option B — Encrypted Export/Import Package

Provide a “Share database profile” button in GRC Financial Control that emits an encrypted JSON payload. Invoice Planner exposes a complementary “Import profile” action. This is useful when the customers have no access to Azure resources or require fully offline distribution.

**Implementation outline**
1. Serialize the database settings into JSON.
2. Encrypt the byte payload with `ProtectedData.Protect` on Windows or ASP.NET Core Data Protection on other platforms. Both options rely on strong OS-provided key storage while keeping the implementation dependency-free.[^2]
3. Wrap the ciphertext with a short-lived passphrase or operator signature so recipients can validate authenticity.
4. Invoice Planner decrypts using the same API and writes the values into its local settings store (e.g., SQLite) before instantiating `ApplicationDbContext`.

**Operational considerations**
- Requires an explicit operator action to export and send the file.
- Best paired with audit logging so you know who generated each package.
- Rotating database credentials still demands redistributing a new package, so schedule periodic reminders.

## Roll-out Checklist
- [ ] Decide whether the organisation can leverage Azure resources; if not, fall back to the export/import flow.
- [ ] For Option A, assign Azure AD app registrations to both desktop apps and document renewal steps.
- [ ] For Option B, document where encrypted packages are stored and how long they remain valid.
- [ ] Update onboarding manuals for Invoice Planner operators to cover the chosen sync approach.

---

**References**
[^1]: [What is Azure App Configuration?](https://learn.microsoft.com/en-us/azure/azure-app-configuration/overview)
[^2]: [ProtectedData Class (System.Security.Cryptography)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)
