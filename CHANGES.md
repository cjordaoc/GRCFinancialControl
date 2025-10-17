# Recent Structural Changes

- Dataverse support was removed. Both the Financial Control and Invoice Planner applications now operate exclusively against the MySQL backend.
- Settings were simplified to focus on MySQL connectivity; Dataverse-specific options and provisioning flows are no longer available.
- Added a new Tasks workspace that generates the weekly administrative JSON template with the scheduled send date automatically set to the next Monday.
