# Recent Structural Changes

- Settings now provide a dedicated "Drop and Recreate Dataverse Tables" action that fully rebuilds the Dataverse schema from the shared metadata definition, clearing prior tables and relationships before reapplying the canonical configuration.
- The Invoice Planner desktop application always runs against Dataverse; SQL/MySQL fallbacks and repository stubs were removed to keep the workflow Dataverse-only.
- Dataverse provisioning now deletes any existing custom tables and relationships derived from the metadata file before recreating them, ensuring a clean rebuild that mirrors the MySQL foreign keys in Dataverse.
