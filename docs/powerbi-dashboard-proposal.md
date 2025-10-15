# Power BI Single-Page Dashboard Proposal

## Objectives and audience
- **Purpose:** Deliver a single, glanceable control panel that helps GRC leadership monitor engagement profitability, delivery performance, and staffing contributions without relying on the Avalonia charts that are currently unreliable.
- **Target users:** Delivery leads, engagement managers, and finance analysts who need to understand engagement health, resource usage, and exceptions before month-end close.
- **Primary questions to answer:**
  1. Are engagements tracking to revenue, margin, and hours targets?
  2. Where do allocations and actual work diverge enough to trigger intervention?
  3. Which PAPD teams and managers contribute the most value or require support?
  4. What exceptions or data quality issues should be resolved immediately?

These objectives map directly to the data already curated in MySQL through our EF Core models (engagement master data, financial evolution snapshots, allocations, actuals, and exceptions).【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L9-L246】【F:GRCFinancialControl.Core/Models/Engagement.cs†L10-L101】

## Source tables and semantic model
Connect Power BI to the operational MySQL database using DirectQuery for near real-time monitoring, or Import mode with scheduled refresh if latency under an hour is acceptable. Recommended entities:

| Business domain | Table / view | Key fields | Usage in dashboard |
| --- | --- | --- | --- |
| Engagement master | `Engagements`, `Customers` | Description, customer, currency, status, ETC values, opening budgets | Card KPIs, slicers, detail tables.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L9-L119】【F:GRCFinancialControl.Core/Models/Engagement.cs†L10-L27】 |
| Financial performance | `FinancialEvolutions`, `ClosingPeriods` | Period, revenue, hours, margin, expenses | Trend lines, variance calculations.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L260-L296】【F:GRCFinancialControl.Core/Models/FinancialEvolution.cs†L5-L13】【F:GRCFinancialControl.Core/Models/ClosingPeriod.cs†L8-L15】 |
| Resource planning | `PlannedAllocations`, `EngagementFiscalYearAllocations`, `EngagementFiscalYearRevenueAllocations` | Planned hours/value by period | Planned vs. actual visuals and KPI cards.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L198-L334】【F:GRCFinancialControl.Core/Models/PlannedAllocation.cs†L8-L14】 |
| Actual delivery | `ActualsEntries` | Hours worked by date, PAPD, closing period | Actual hours trend and variance.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L212-L234】【F:GRCFinancialControl.Core/Models/ActualsEntry.cs†L8-L18】 |
| PAPD contribution | Derived from `EngagementPapds` + `FinancialEvolutions` via report service logic | Revenue contribution, hours by period | Contribution donut/line combos.【F:GRCFinancialControl.Persistence/Services/ReportService.cs†L25-L178】【F:GRCFinancialControl.Core/Models/Reporting/PapdContributionData.cs†L3-L14】 |
| Exceptions | `Exceptions` | Source file, reason | Exception watchlist table.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L235-L241】 |

Create a star schema in Power BI:
- **Fact tables:** Financial evolution snapshots (period grain), actual hours (daily/period grain), planned allocations (period grain).
- **Dimensions:** Engagement, Customer, PAPD, Manager, Closing Period, Fiscal Year.
- Use calculation groups/measures for common metrics (current period revenue, margin %, variance vs. plan, ETC fulfillment). Align period slicers using the Closing Period dimension to keep charts synchronized.【F:GRCFinancialControl.Persistence/Services/ReportService.cs†L229-L295】

## Layout blueprint (single page, 16:9 canvas)
Apply Microsoft dashboard design guidance: keep essential KPIs in the upper-left, avoid clutter, and use consistent scales and chart types to emphasize comparisons.【cebdc4†L1-L107】 Draw inspiration from Microsoft's financial workbook sample, which surfaces statements, KPIs, and supporting visuals on one canvas.【7c46c5†L1-L28】

1. **Header bar (top row, full width):**
   - Filters: Date range (Closing Period), Engagement, Customer, Manager.
   - Title, refresh timestamp (from dataset metadata), contact information.
2. **Executive KPI strip (top-left cards):**
   - **Total Revenue (current period)** and **Revenue vs. Plan %** using `FinancialEvolutions.ValueData` compared to planned value measures.【F:GRCFinancialControl.Core/Models/FinancialEvolution.cs†L8-L11】【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L315-L333】
   - **Realized Margin %** from `FinancialEvolutions.MarginData` with color status indicators.【F:GRCFinancialControl.Core/Models/FinancialEvolution.cs†L10-L11】
   - **Actual Hours vs. Planned Hours** using `ActualsEntries.Hours` vs. `PlannedAllocations.AllocatedHours` to highlight utilization.【F:GRCFinancialControl.Core/Models/ActualsEntry.cs†L11-L12】【F:GRCFinancialControl.Core/Models/PlannedAllocation.cs†L11-L14】
   - **Open Exceptions Count** (rows in `Exceptions`).【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L235-L241】
3. **Financial evolution trend (top-right half-width):** Dual-axis line chart showing revenue and expenses over closing periods with margin as tooltip, leveraging `FinancialEvolutionPoint` data.【F:GRCFinancialControl.Core/Models/Reporting/FinancialEvolutionPoint.cs†L5-L15】 Keep axis scales consistent per best practices.【cebdc4†L83-L104】
4. **Hours performance (mid-left):** Column chart comparing planned vs. actual hours by closing period, with data labels suppressed to reduce clutter and focus on relative differences.【F:GRCFinancialControl.Core/Models/PlannedAllocation.cs†L11-L14】【F:GRCFinancialControl.Core/Models/ActualsEntry.cs†L11-L17】【cebdc4†L83-L105】
5. **PAPD contribution (mid-right):**
   - Donut or stacked bar showing revenue contribution by PAPD (limit to top contributors to avoid readability issues).【F:GRCFinancialControl.Core/Models/Reporting/PapdContributionData.cs†L3-L14】【cebdc4†L83-L100】
   - Small multiples or line chart of hours worked by PAPD across periods to identify sustained effort or drop-offs (use consistent color palette for each PAPD across visuals).【F:GRCFinancialControl.Persistence/Services/ReportService.cs†L98-L175】【cebdc4†L83-L104】
6. **Engagement health matrix (lower-left):** Table or matrix listing engagements with key columns: Customer, Revenue variance, Margin variance, Hours variance, ETC age (from `Engagement.EtcpAgeDays`) and flags for `RequiresAllocationReview`/`RequiresRevenueAllocationReview`. Conditional formatting to prioritize items needing action.【F:GRCFinancialControl.Core/Models/Engagement.cs†L23-L100】
7. **Exception & data quality log (lower-right):** Table summarizing unresolved exceptions with source file, reason, created date to drive remediation.【F:GRCFinancialControl.Persistence/ApplicationDbContext.cs†L235-L241】

## Visual and UX considerations
- Maintain a consistent color palette (corporate colors + status colors) and align with Microsoft guidance to avoid 3D or overly novel visuals.【cebdc4†L83-L104】
- Ensure all visuals fit without vertical scrolling; use the page navigator for drill-through to detailed reports if needed, keeping the primary dashboard to one screen as recommended.【cebdc4†L61-L78】
- Provide descriptive titles, subtitles, and tooltips to give context, similar to the structure highlighted in Microsoft's sample dashboards.【7c46c5†L1-L28】【cebdc4†L83-L107】

## Measures and calculations
Key DAX measures to implement:
- `Revenue (Current Period)` = SUM of `FinancialEvolutions[ValueData]` filtered to selected period.
- `Revenue vs Plan %` = `(Revenue - Planned Revenue) / Planned Revenue` using planned allocations.
- `Margin %` = AVERAGE of `FinancialEvolutions[MarginData]` (or weighted by revenue).
- `Hours Variance` = `SUM(Actual Hours) - SUM(Planned Hours)`.
- `Exception Count` = COUNTROWS of `Exceptions` with status <> Resolved.
- `ETC Age` = DAX measure referencing last ETC date to compute age in days (or import `EtcpAgeDays`).【F:GRCFinancialControl.Core/Models/Engagement.cs†L45-L100】

## Interactivity and drill-through
- Enable drill-through on engagement name to a detailed report page showing monthly breakdowns, manager assignments, and backlog details.
- Use tooltip pages for contextual comparisons (e.g., show previous period revenue/margin when hovering over trend points).
- Provide bookmarks for key personas (Finance vs. Delivery) to emphasize relevant filters and metrics.

## Implementation roadmap
1. **Dataset setup:** Configure Power BI gateway connection to MySQL, create parameterized connection strings, and define incremental refresh if data volume is high.
2. **Modeling:** Import required tables, define relationships (Engagement ↔ FinancialEvolution via `EngagementId`, Engagement ↔ Actuals, Engagement ↔ Allocations, ClosingPeriod ↔ facts). Add calculated tables for date/calendar alignment if needed.
3. **Measure development:** Implement DAX measures above, ensuring consistent formatting and scaling (thousands/millions) per Microsoft recommendations.【cebdc4†L83-L105】
4. **Report design:** Lay out visuals according to the blueprint; align to a grid, test responsiveness in Power BI desktop and service full-screen mode.【cebdc4†L79-L87】
5. **Validation:** Cross-check totals with existing Avalonia reports and financial exports to confirm parity.
6. **Deployment:** Publish to Power BI Service workspace, configure access, and set up alert subscriptions (e.g., margin below threshold) leveraging Power BI's alert feature highlighted in the service documentation.【cebdc4†L1-L78】

Following this plan delivers a Power BI experience that mirrors the intent of the Avalonia dashboard while taking advantage of Power BI's proven design practices and interactive capabilities.【cebdc4†L1-L107】
