# Unified GUI Design Guidelines

These guidelines define the shared Avalonia MVVM experience for **GRC Financial Control** and **Invoice Planner**. All new or refactored UI must conform to this document so both applications deliver a consistent desktop shell.

## 1. Scope & Principles
- **Single source of truth**: Reuse resources from `GRC.Shared.Resources/Theme` and shared UI helpers before adding new styles.
- **MVVM separation**: Views declare layout only; interaction and state belong in ViewModels using CommunityToolkit.Mvvm attributes (`[ObservableProperty]`, `[RelayCommand]`).
- **Compiled bindings**: Set `x:DataType` on every `UserControl`/`Window` to enable compile-time binding validation.
- **Localization-first**: All text strings resolve through the shared `loc:Loc` markup extension pointing to centralized resources (see Section 6).
- **Responsive clarity**: Favor flexible layout containers (`Grid`, `StackPanel`) that adapt to window resizing. Avoid fixed pixel sizes other than shared tokens.
- **Accessibility**: Maintain high-contrast colors via shared brushes, ensure focus states remain visible, and validate tab order using `TabIndex`/`IsTabStop` for full keyboard navigation.

## 2. Solution Structure & Naming
- `GRC.Shared.UI` hosts documentation, shared control templates, and future composite controls. Place human-readable design assets in `GRC.Shared.UI/Documentation`.
- `Views` mirror `ViewModels` one-to-one (e.g., `Views/HomeView.axaml` ↔ `ViewModels/HomeViewModel.cs`).
- Use PascalCase for files and classes (`InvoicePlannerEntryView.axaml`); suffix dialogs with `DialogView` and flyouts with `FlyoutView`.
- Group feature-specific components in folders under `Views/<Feature>` and mirror the structure in `ViewModels/<Feature>`.
- When introducing reusable UI pieces (custom controls, converters, behaviors), locate them in shared projects (`GRC.Shared.UI`, `GRC.Shared.Resources`) instead of app-specific folders.

## 3. Layout Patterns
- Root containers use `Grid` with explicit `RowDefinitions`/`ColumnDefinitions` where alignment matters. Apply `RowSpacing`/`ColumnSpacing` via shared spacing tokens.
- Page content typically sits in a `ScrollViewer` wrapping a `StackPanel` with `Spacing="{StaticResource Spacing16}"` and `Margin="{StaticResource SpaceThickness16}"` or `Margin="{StaticResource MarginLarge}"` for dashboards (see `GRCFinancialControl.Avalonia/Views/HomeView.axaml`).
- Cards and grouped sections reside inside `<Border Classes="Card">` to inherit background, border, and shadow settings defined in `App.Presentation/Styles/Styles.xaml`.
- Align form labels and inputs using two-column `Grid` layouts where needed. Use `HorizontalAlignment="Stretch"` on inputs to fill available space.
- Avoid nested `StackPanel` hierarchies deeper than two levels. Prefer `Grid` or `UniformGrid` for complex arrangements.

## 4. Spacing & Sizing Tokens
Use the following shared measurements (defined in `GRC.Shared.Resources/Theme/Spacing.axaml` and `.../Dimensions.axaml`):
- **Spacing**: `Spacing4`, `Spacing8`, `Spacing12`, `Spacing16`, `Spacing24` cover most vertical/horizontal gaps.
- **Margins/Thickness**: `MarginSmall`, `MarginMedium`, `MarginLarge`, `SpaceThickness16`, `SpaceThickness24` standardize padding.
- **Input heights**: `InputHeightStandard` for TextBox, ComboBox, DatePicker (see `App.Presentation/Styles/Styles.xaml`).
- **Row height**: `RowHeightStandard` governs DataGrid rows and list items.
When special spacing is required, add it to the shared resource dictionaries rather than hard-coding numbers.

## 5. Typography & Color
- Default font family is `FontPrimary` (`EYInterstate`) with sizes from `FontSize12` to `FontSize24` (`GRC.Shared.Resources/Theme/Typography.axaml`).
- Apply semantic TextBlock classes: `TitleLarge`, `TitleMedium`, `TitleSmall`, `Caption` for section headings and helper text (`App.Presentation/Styles/Styles.xaml`).
- Colors come from `GRC.Shared.Resources/Theme/Colors.axaml`. Use brush keys such as `BrushSurface`, `BrushSurfaceVariant`, `BrushPrimary`, `BrushOnSurface`, `BrushSuccess`, `BrushWarning`, `BrushError`.
- Align palettes with the EY brand direction: Primary Yellow accents paired with Deep Gray neutrals. Apply rounded corners and elevation reminiscent of Microsoft Fluent design for consistency with Avalonia defaults.
- Keep contrast by pairing surface brushes with appropriate `BrushOn*` values. Never create ad-hoc color hex codes in views.

## 6. Localization Usage
- Use `xmlns:loc="clr-namespace:App.Presentation.Localization;assembly=App.Presentation"` and `Text="{loc:Loc Key=...}"`.
- Keys must map to centralized resource files (`Strings.resx`, `Strings.pt-BR.resx`, `Strings.en-US.resx`) once consolidated in Stage 3.
- Simplify keys to descriptive nouns or verbs (e.g., `Common.Button.Save`). Avoid suffixes like `_Text` or `_Label` unless contextually necessary.
- For formatted strings, rely on `LocalizationRegistry.Format("Key", args...)` in ViewModels.
- When formatting numbers, currencies, or dates, read culture info from `CultureInfo.CurrentUICulture` so pt-BR and en-US remain accurate.

## 7. Component Standards
### Buttons
- Base `Button` style comes from `GRC.Shared.Resources/Theme/Controls.axaml` (height, padding, corner radius).
- Use `Classes` for variants (`accent`, `primary`, etc.) when defined; otherwise, rely on the base style. Maintain minimum widths of 120–160px for primary actions.
- Group buttons inside horizontal `StackPanel` with `Spacing="{StaticResource Spacing16}"`.

### DataGrid
- Set `AutoGenerateColumns="False"` and define explicit bindings.
- Apply shared header styling automatically via `Styles.xaml`. Ensure `RowHeight` uses `RowHeightStandard` and `HeadersVisibility="Column"`.
- Wrap long text columns with `CellStyle` or the shared `DataGridWrappedTextStyle` resource from `App.Presentation/Styles/Styles.xaml`.
- Keep DataGrids inside containers that provide `Margin="{StaticResource MarginMedium}"` at minimum.

### Forms & Inputs
- Align labels above inputs unless side-by-side layout improves readability. Use `FontWeightSemiBold` for labels.
- Indicate required fields via helper text rather than inline asterisks when possible.
- Use `MinWidth` tokens from shared resources for ComboBox and DatePicker controls.

### Tabs & Navigation
- Tab headers follow the `TabItem` style defined in `App.Presentation/Styles/Styles.xaml`. Use shared localization keys for tab text (e.g., `Common.Tab.General`).
- Sidebars use `ToggleButton` with `NavButton` style and share spacing tokens for icon/text alignment.

#### Sidebar Collapse Behavior
- Initialize sidebars in the expanded state so navigation options remain discoverable after launch.
- Hovering over a collapsed sidebar expands it with an `Expander` control or `Transition`-driven width animation for smoothness.
- Bind both applications to a shared `IsSidebarExpanded` property in their ViewModels and persist the value to keep the experience consistent across sessions.

### Dialogs & Modals
- Encapsulate modal content within `<Border Classes="ModalDialog">` defined in `GRC.Shared.Resources/Theme/Controls.axaml`.
- Overlay/backdrop behavior derives from `App.Presentation/Styles/ModalOverlay.xaml`. Reuse this pattern for all dialogs.
- Maintain generous padding (`MarginLarge`) inside modals and align action buttons right-aligned within the footer.

## 8. UI Responsiveness Breakpoints
- Monitor window width via `Bounds.Width` updates in the ViewModel or `AdaptiveTrigger`-like behaviors within the view layer.
- Categorize layouts as **Compact** (<960px), **Regular** (960–1365px), or **Wide** (≥1366px).
- Adjust spacing tokens, column definitions, and optional panels per category to keep both apps responsive without duplicating XAML.

## 9. Effects & Elevation
- Shadows originate from `GRC.Shared.Resources/Theme/Effects.axaml` and `Elevation.axaml`. Use `ShadowLow` for cards, `ShadowHigh` for dialogs.
- Acrylic backgrounds are reserved for overlays and modal surfaces; verify tokens before applying new effects.

## 10. Interaction Patterns
- Commands expose async operations via `[RelayCommand]` methods returning `Task`. Ensure `CanExecute` guards match view state (see `HomeViewModel` for sample usage).
- Display busy state through `IsBusy` boolean bound to `ProgressBar`/`ProgressRing` components and disabling inputs.
- Messaging between views uses `IMessenger` from CommunityToolkit; publish notifications centrally to avoid code-behind logic.

## 11. Folder & Resource Organization
- Resource dictionaries live in `GRC.Shared.Resources/Theme`. Extend them with new tokens instead of scattering `.axaml` files in application projects.
- Include shared converters, behaviors, or attached properties in `App.Presentation` until a dedicated shared UI assembly is introduced.
- Documentation (design notes, diagrams) belongs under `GRC.Shared.UI/Documentation` with Markdown format.

## 12. Review Checklist for New Views
1. View file located under the correct feature folder with matching ViewModel.
2. `x:DataType` declared and no code-behind logic beyond constructor `InitializeComponent()`.
3. Uses shared spacing, typography, and colors—no magic numbers or inline hex.
4. All strings resolved through localization resources.
5. Buttons, DataGrids, Tabs, and dialogs follow the component standards listed above.
6. Layout tested for resizing; no clipped content at 1024×768.
7. Shared resources imported at the view root (`<UserControl.Styles> <StyleInclude Source="avares://App.Presentation/Styles/Styles.xaml" /> ...` when necessary).
8. ViewModel respects MVVM boundaries, uses dependency-injected services, and exposes observable properties/commands.

Adhering to these guidelines keeps **GRC Financial Control** and **Invoice Planner** visually aligned, maintainable, and ready for future shared UI investments.

---

## Reference Material
- [Avalonia MVVM Pattern](https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/)
- [Avalonia Performance & UI Responsiveness](https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance/)
- [Avalonia Styling & Resource Dictionaries](https://docs.avaloniaui.net/docs/guides/development-guides/styling/)
- [CommunityToolkit.Mvvm Documentation](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
