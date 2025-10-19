# Temporary I18n Plan (Stage 5a — remove in Stage 7)

## Scope Inventory
- **Invoice Planner Avalonia** — navigation shell, invoice plan workflow (editor, summary, notifications, request/emission confirmations), connection setup, access restriction prompts, shared dialogs.
- **GRC Financial Control Avalonia** — dashboards, administration grids (PAPDs, managers, engagements), fiscal data management, settings/about surfaces.
- **Shared presentation assets** — styles already centralized; no shared localization helpers beyond `Strings` class in Invoice Planner (will be generalized).

## UI Text Categories
- **Navigation & menus** — module tabs, flyouts, settings/about entries.
- **Headings & section labels** — page titles, group headers, table column headers.
- **Input labels & descriptions** — form field captions, helper text, inline explanations.
- **Buttons & commands** — primary/secondary actions, toolbar buttons, confirmation choices.
- **Watermarks & placeholders** — TextBox, ComboBox, SearchBox watermarks.
- **Status, toast, and inline messages** — progress text, success/error info banners, assignment guards.
- **Dialogs & pop-ups** — titles, body copy, confirmation prompts, error details.
- **Validation & user feedback** — field-level validation errors, blocking alerts.
- **Empty states** — default copy when lists/grids have no data, first-run hints.
- **Generated document text** — PDF/Excel headers, notification templates, exported worksheet labels.

## Resource Structure
- Maintain **per-app resource folders** (`InvoicePlanner.Avalonia/Resources`, `GRCFinancialControl.Avalonia/Resources`).
- Introduce a shared helper (extension) in `App.Presentation` so both apps resolve localized strings consistently.
- Use **`Strings.en-US.resx`** as the canonical resource in each app; keep neutral `Strings.resx` as build artifact pointing to the same set via code-gen.
- Mirror the canonical keys into `Strings.pt-BR.resx` and `Strings.es-PE.resx` once created.
- Store any shared business text (e.g., export headers reused by both apps) under a future `App.Presentation/Resources/Strings.*.resx` only if duplication appears during extraction.

## Key Naming Scheme
- Keys follow the pattern `Area.Category.Name`, using PascalCase segments.
  - **Area** — feature or screen (`Shell`, `Navigation`, `InvoicePlan`, `Settings`, `Admin.Managers`, `Admin.Papds`, `Exports.InvoiceSummary`).
  - **Category** — UI element type (`Title`, `Description`, `Button`, `Label`, `Placeholder`, `Dialog`, `Message`, `Status`, `Validation`).
  - **Name** — specific meaning (`Primary`, `Success`, `NoAssignments`, `ImportFailure`).
- Common/shared keys start with `Common.` (e.g., `Common.Button.Save`, `Common.Status.Loading`).
- Generated artifacts append granular suffixes (`Exports.InvoiceSummary.Header.Customer`, `Notifications.Request.Subject`).

## Execution Notes
- Refactor existing Invoice Planner `Strings` usage to the new key pattern before extracting additional text.
- Introduce analogous resource access infrastructure in GRC Financial Control before moving its UI text.
- Ensure bindings use the same markup extension for both apps once shared.
- Documented plan will be deleted during Stage 7 after verifying implementation.
