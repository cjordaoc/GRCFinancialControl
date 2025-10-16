# Dataverse Changes Plan

## Overview

This document summarizes the actions required to align the Dataverse schema and application runtime with the authoritative MySQL definition contained in `artifacts/mysql/rebuild_schema.sql`. The plan builds on the outputs from the `DvSchemaSync` CLI dry-run and highlights how native Dataverse capabilities replace redundant custom columns while keeping the existing MySQL implementation untouched.

## Guiding Principles

- Preserve the MySQL stack in its current state; the Dataverse migration must remain isolated behind the `DATA_BACKEND` toggle.
- Prefer native Dataverse attributes such as `createdon`, `modifiedon`, `ownerid`, `statecode`, and `statuscode` when they provide the same semantics as custom MySQL columns.
- Apply schema modifications in a safe order: add or update first, export solutions for backup, and only drop unused artifacts when explicitly allowed.
- Keep deletions optional (`DVSCHEMA_ALLOW_DROP=1`) and conditioned on dependency analysis to avoid breaking managed solutions, views, or forms.

## Schema Alignment Roadmap

1. **Add Missing Attributes**
   - Introduce the attribute set identified by the dry-run (currently 95 columns) so Dataverse entities expose the same data points as their MySQL counterparts.
   - Normalize datetime and boolean columns to Dataverse-friendly types and map lookup fields to the proper reference entities.

2. **Adopt Native Replacements**
   - Redirect `CreatedAt`, `UpdatedAt`, and similar tracking fields to native Dataverse audit columns through the native field replacement map.
   - Replace bespoke status columns with `statecode`/`statuscode` option sets wherever workflow semantics align.

3. **Establish Alternate Keys and Relationships**
   - Create the 28 alternate keys and 18 relationships surfaced by the planner to match composite unique constraints and foreign keys defined in MySQL.
   - Confirm each relationship uses the canonical lookup attribute names registered in the metadata registry to keep repository implementations consistent.

4. **Evaluate Missing Entities**
   - Provision the 16 entities reported as missing when they represent true tables in MySQL that do not yet exist in Dataverse.
   - For entities already implemented via native constructs (e.g., activity feeds), document the rationale in `docs/dv_alignment_report.md` and mark them as resolved.

5. **Review Deletion Candidates**
   - Inspect `docs/dv_delete_candidates.json` to confirm that no redundant custom attributes remain after native substitutions.
   - Only execute deletion steps with `--apply --allow-drop` when `DVSCHEMA_ALLOW_DROP=1` and the `--solution-export` backup has been taken.

## Application Readiness Tasks

1. **Dataverse Backend Validation**
   - Run the Avalonia applications with `DATA_BACKEND=Dataverse` to ensure read/write flows operate through the new repositories.
   - Enable `DV_ENRICH_PEOPLE=1` in a connected environment to verify system user resolution and caching behaviour.

2. **MySQL Regression Guardrails**
   - Continue to exercise the MySQL backend by building and running the apps without setting `DATA_BACKEND` (default MySQL path).
   - Keep CI building both configurations to detect accidental cross-backend regressions early.

3. **Documentation & Support**
   - Maintain `docs/dv_alignment_report.md` and `docs/dv_delete_candidates.json` via the CLI after schema updates.
   - Use this plan alongside the README instructions to guide Dataverse administrators through staged deployments.

## Execution Checklist

- [ ] `dotnet run --project tools/DvSchemaSync -- --dry-run`
- [ ] Review/update `docs/dv_alignment_report.md`
- [ ] Review/update `docs/dv_delete_candidates.json`
- [ ] Prepare Dataverse credentials (`DV_CLIENT_ID`, `DV_CLIENT_SECRET`, `DV_TENANT_ID`, `DV_ORG_URL`)
- [ ] Run with `--apply` (adds/updates only)
- [ ] Export solution backup (`--solution-export ExampleSolution`)
- [ ] (Optional) Enable drops: `DVSCHEMA_ALLOW_DROP=1 --allow-drop`
- [ ] Validate application flows on Dataverse backend
- [ ] Validate application flows on MySQL backend

