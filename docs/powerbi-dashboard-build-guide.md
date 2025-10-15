# Power BI Dashboard Build Guide

This guide explains how to configure the provided Power BI project (`powerbi/GRCFinancialControlDashboard`) so it can be embedded in the Avalonia client.

## Prerequisites

- Power BI Desktop June 2024 or newer.
- Access to the MySQL instance that backs the GRC Financial Control application.
- The **Power BI Project (.pbip) save option** preview feature enabled in Power BI Desktop (File → Options and settings → Options → Preview features).

## Configure the semantic model

1. Clone or pull the repository and open the `powerbi/GRCFinancialControlDashboard` folder.
2. Open `GRC Financial Control Dashboard.pbip` in Power BI Desktop.
3. In the **Data** pane, Power BI prompts for MySQL credentials. Provide the same server, database, and login values captured in the Avalonia Settings screen.
4. Edit the M partition in *Model view → Semantic model → Table: Fiscal Performance → Partitions* and replace the placeholders:
   - `{{MYSQL_SERVER}}` → MySQL host or IP address.
   - `{{MYSQL_DATABASE}}` → Database that contains the GRC Financial Control schema.
5. Refresh the model. The DirectQuery statement aggregates:
   - Actual hours by fiscal year based on `ActualsEntries` and `ClosingPeriods`.
   - Planned hours (`EngagementFiscalYearAllocations`).
   - Planned revenue (`EngagementFiscalYearRevenueAllocations`).
   - Gap calculations versus the fiscal year targets stored on `FiscalYears`.

## Compose the dashboard visuals

The semantic model already exposes the measures required by the proposal:

- `Actual Hours`
- `Target Hours`
- `Hours Gap to Target`
- `Hours Completion %`
- `Planned Revenue`
- `Area Revenue Target`
- `Revenue Gap to Target`
- `Revenue Completion %`

Use them to build the single page layout in Power BI Desktop:

1. Add a clustered column chart with **Fiscal Year** on the axis and both `Actual Hours` and `Target Hours` as values.
2. Add a bar (or waterfall) chart bound to `Hours Gap to Target` so management can see how far each fiscal year is from plan.
3. Add cards for `Hours Completion %` and `Revenue Completion %` for at-a-glance status.
4. Optionally add a matrix with **Fiscal Year**, `Actual Hours`, `Target Hours`, `Planned Revenue`, and `Revenue Gap to Target` for detail.
5. Save the project. Power BI Desktop keeps the semantic model inside the repository so it can be embedded directly from the Avalonia application.

## Publish options

- **Publish to Web**: Create a publish-to-web link and store it in the Avalonia Settings screen (`PowerBiEmbedUrl`). This works for broad sharing but exposes data publicly.
- **App owns data**: Publish to a Fabric workspace and capture the workspace (`groupId`) and report (`reportId`). Configure a service principal to generate embed tokens and store them (`PowerBiWorkspaceId`, `PowerBiReportId`, `PowerBiEmbedToken`) in the application settings.

Once published, the Reports view WebView will load the configured URL and provide a consistent embedded experience.
