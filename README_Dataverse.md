# GRC Financial Control — Dataverse Edition

This guide explains how to build and run the Dataverse-backed variant of GRC Financial Control alongside the existing MySQL implementation. Follow these instructions when working in the `feature/dataverse-migration` branch or its descendants.

## Prerequisites

- .NET 8 SDK
- Access to the Dataverse environment at `https://org88f764c1.crm2.dynamics.com`
- An Azure AD application with client credentials for Dataverse Web API access
- MySQL server (for baseline builds) and SQLite (already included) for local caches

## Required Environment Variables

Set the following variables before running the Dataverse application or schema tooling:

| Variable | Purpose |
| --- | --- |
| `DATA_BACKEND` | Selects the data provider (`Dataverse` or `MySQL`). |
| `DV_ORG_URL` | Organization URL (e.g., `https://org88f764c1.crm2.dynamics.com`). |
| `DV_TENANT_ID` | Azure AD tenant ID hosting the Dataverse environment. |
| `DV_CLIENT_ID` | Client ID of the Azure AD app registration. |
| `DV_CLIENT_SECRET` | Client secret for the app registration. |
| `DV_ENRICH_PEOPLE` | Optional (`0`/`1`) toggle for system user enrichment. |
| `DVSCHEMA_ALLOW_DROP` | Optional (`0`/`1`) toggle that permits schema deletions when running the CLI. |

Export the variables in your shell (example for bash):

```bash
export DATA_BACKEND=Dataverse
export DV_ORG_URL="https://org88f764c1.crm2.dynamics.com"
export DV_TENANT_ID="00000000-0000-0000-0000-000000000000"
export DV_CLIENT_ID="11111111-1111-1111-1111-111111111111"
export DV_CLIENT_SECRET="<client-secret>"
export DV_ENRICH_PEOPLE=1
```

> Leave `DVSCHEMA_ALLOW_DROP` unset (or `0`) unless you intend to remove Dataverse attributes or keys.

## Building the Applications

1. Restore dependencies (if needed):
   ```bash
   dotnet restore GRCFinancialControl.sln
   ```
2. Build the solution for both backends:
   ```bash
   dotnet build GRCFinancialControl.sln
   DATA_BACKEND=Dataverse dotnet build GRCFinancialControl.sln
   ```

The first command validates the legacy MySQL backend; the second command confirms the Dataverse variant compiles with the substituted services.

## Running the Avalonia Applications

Launch either Avalonia application with the Dataverse backend enabled:

```bash
DATA_BACKEND=Dataverse dotnet run --project GRCFinancialControl.Avalonia
```

The host will resolve Dataverse services, connect using the credentials above, and respect the `DV_ENRICH_PEOPLE` toggle to backfill system user names.

To switch back to MySQL, unset `DATA_BACKEND` or set it to `MySQL` before running the application.

## Using the Dataverse Schema Sync CLI

The CLI keeps Dataverse metadata aligned with the authoritative MySQL SQL script. Commands should be executed from the repository root unless `--sql`, `--output`, or `--delete-candidates` override the defaults.

- Dry-run and report (no changes applied):
  ```bash
  dotnet run --project tools/DvSchemaSync -- --dry-run
  ```
- Apply additions/updates after reviewing the dry-run:
  ```bash
  dotnet run --project tools/DvSchemaSync -- --apply --solution-export GRCFinancialControl_DV_Backup
  ```
- Permit deletions (requires environment flag):
  ```bash
  export DVSCHEMA_ALLOW_DROP=1
  dotnet run --project tools/DvSchemaSync -- --apply --allow-drop --solution-export GRCFinancialControl_DV_Backup
  ```

Outputs:
- `docs/dv_alignment_report.md` — alignment details for every table/column/key.
- `docs/dv_delete_candidates.json` — structured list of safe-to-drop candidates.

## Troubleshooting

- **Authentication failures**: Confirm the Azure AD application has Dataverse API permissions and the client secret has not expired.
- **Missing metadata**: The CLI reports "Dataverse metadata could not be retrieved" when credentials are absent; provide the environment variables and rerun.
- **Schema deletions blocked**: Some Dataverse attributes cannot be removed if referenced by forms or managed solutions. Review the deletion candidates and disable drops when necessary.

## Keeping MySQL Green

The MySQL backend remains the default in the original repository. All Dataverse work should stay behind the `DATA_BACKEND` toggle to keep MySQL builds passing. Use the CI workflow or the build commands above to validate both paths before merging.

