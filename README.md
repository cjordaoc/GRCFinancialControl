# GRC Financial Control

The solution delivers the invoice management workflows used by controllers to plan, confirm, and notify billing activities while remaining aligned with the centralized financial data model.

## Core Features
- **Invoice flow:** controllers prepare invoice plans, review confirmation requests, track emission status, and send notification previews from a single workspace backed by the shared domain services in `Invoices.Core` and `Invoices.Data`.
- **Access filtering:** each Avalonia session resolves the signed-in login to an `InvoiceAccessScope`, limiting queries and UI state to the engagements assigned to that user and flagging unassigned logins with localized guidance.
- **Centralized styles:** UI theming and control styling are sourced from `App.Presentation/Styles/Styles.xaml`, keeping both apps visually consistent while avoiding duplication in individual views.
- **Language & i18n:** the Settings → Connection page lets users choose English, Portuguese, or Spanish. The selection persists in the local settings store, and the shared `LocalizationRegistry` resolves all interface text from the resource files in `InvoicePlanner.Avalonia/Resources` at startup.

## Build
Restore dependencies with `dotnet restore` and build with `dotnet build -c Release`.
