# GRC Financial Control Power BI Project

This folder contains a Power BI project (.pbip) skeleton for the single page dashboard proposed in the documentation. The semantic model is authored in TMDL to make it easy to review and edit.

## How to use

1. Install the latest Power BI Desktop (June 2024 or newer) with the Power BI Project preview enabled.
2. Open the project by double-clicking `GRC Financial Control Dashboard.pbip` or by opening the report folder in Power BI Desktop.
3. Update the MySQL connection placeholders in `definition/database.tmdl`:
   - Replace `{{MYSQL_SERVER}}` with your MySQL server host.
   - Replace `{{MYSQL_DATABASE}}` with the database name that hosts the GRC Financial Control schema.
4. When prompted by Power BI Desktop, provide the MySQL credentials that match the settings configured in the application. Store the credentials as an organizational connection.
5. Refresh the semantic model. The DirectQuery SQL statement materializes fiscal year performance, target hours, and planned revenue gap calculations.
6. Build the visuals on the Dashboard page using the curated measures:
   - Clustered column chart with Fiscal Year on the axis and `Actual Hours`/`Target Hours` as values.
   - Bar or waterfall visual using `Hours Gap to Target` to highlight the deficit to plan.
   - Card visuals for `Hours Completion %` and `Revenue Completion %`.
   - Optional matrix with Fiscal Year, `Actual Hours`, `Target Hours`, `Planned Revenue`, and `Revenue Gap to Target`.

## Validate the project structure

Before committing changes run `powerbi/validate_powerbi_project.py` from the repository root. The helper inspects every
`definition.pbism` file to ensure only schema-supported properties are present—matching the guidance in the official Power BI
Projects overview on Microsoft Learn. This prevents unsupported properties such as `displayName` from being reintroduced and
causing Power BI Desktop to reject the project file.

The semantic model already exposes the following measures:

- `Actual Hours`
- `Target Hours`
- `Hours Gap to Target`
- `Hours Completion %`
- `Planned Revenue`
- `Area Revenue Target`
- `Revenue Gap to Target`
- `Revenue Completion %`

These metrics power the comparisons required for monitoring progress against fiscal year targets.

> **Note**: The report definition ships with an empty canvas so the visuals can be composed directly in Power BI Desktop using the measures above. This avoids hard-coding layout metadata that is difficult to maintain outside of Desktop while still providing a ready-to-connect semantic model.
