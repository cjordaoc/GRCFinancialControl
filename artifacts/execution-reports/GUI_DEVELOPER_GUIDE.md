# GRC Shared UI Components - Developer Guide

This guide explains how to use the standardized UI components and styles in the GRC.Shared.UI and GRC.Shared.Resources libraries.

---

## 📚 Table of Contents

1. [Theme System](#theme-system)
2. [Text Styles](#text-styles)
3. [Control Styles](#control-styles)
4. [Dialog Templates](#dialog-templates)
5. [Shared Controls & DataTemplates](#shared-controls--datatemplates)
6. [Best Practices](#best-practices)

---

## Theme System

### Resource Dictionary Structure

```
GRC.Shared.Resources/Theme/
├── Colors.axaml          - Color palette (BrushPrimary, BrushSecondary, etc.)
├── Spacing.axaml         - Layout spacing tokens
├── Dimensions.axaml      - Size tokens (button heights, widths, etc.)
├── Typography.axaml      - Font resources (FontPrimary, FontSizeBody, etc.)
├── TextStyles.axaml      - Text block styles (TitleLarge, Caption, etc.)
├── Controls.axaml        - Control styles (Button, DataGrid, Card, etc.)
├── Shapes.axaml          - Border radius, corner tokens
├── Elevation.axaml       - Shadow definitions
├── Effects.axaml         - Visual effects
├── Icons.axaml           - Icon path geometries
└── Theme.axaml           - Master merge (imports all resource dictionaries)
```

### Using the Theme in Your Application

**App.axaml Configuration**:
```xml
<Application xmlns="https://github.com/avaloniaui"
             RequestedThemeVariant="Dark">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- Import all resources (colors, spacing, fonts, etc.) -->
                <ResourceInclude Source="avares://GRC.Shared.Resources/Theme/Theme.axaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
    
    <Application.Styles>
        <FluentTheme/>
        <!-- Import control styles -->
        <StyleInclude Source="avares://GRC.Shared.Resources/Theme/Controls.axaml"/>
        <!-- Import text styles -->
        <StyleInclude Source="avares://GRC.Shared.Resources/Theme/TextStyles.axaml"/>
        <!-- Import application-specific styles -->
        <StyleInclude Source="avares://App.Presentation/Styles/Styles.xaml"/>
    </Application.Styles>
</Application>
```

---

## Text Styles

### Available Text Styles

| Style Class | Font Size | Font Weight | Use Case |
|-------------|-----------|-------------|----------|
| `TitleLarge` | 18 (FontSizeTitle) | SemiBold | Page titles, main headings |
| `TitleMedium` | 16 (FontSizeSection) | SemiBold | Section headers, card titles |
| `TitleSmall` | 14 (FontSizeBody) | SemiBold | Sub-sections, list headers |
| `TitleXSmall` | 12 (FontSizeCaption) | SemiBold | Micro titles, labels |
| `Caption` | 12 (FontSizeCaption) | Regular | Secondary text, hints, metadata |
| `SidebarTitle` | Inherited | SemiBold | Navigation sidebar items |
| `StatusSuccess` | 14 | SemiBold (Green) | Success messages |
| `StatusError` | 14 | SemiBold (Red) | Error messages |

### Usage Examples

```xml
<!-- Page Title -->
<TextBlock Classes="TitleLarge" Text="Engagements Management" />

<!-- Section Header -->
<TextBlock Classes="TitleMedium" Text="Recent Allocations" />

<!-- List Item Title -->
<TextBlock Classes="TitleSmall" Text="Engagement #12345" />

<!-- Caption / Metadata -->
<TextBlock Classes="Caption" Text="Last updated: 2 hours ago" />

<!-- Status Messages -->
<TextBlock Classes="StatusSuccess" Text="Import completed successfully" />
<TextBlock Classes="StatusError" Text="Failed to save changes" />
```

### Resource Tokens

You can also use the underlying font resources directly:

```xml
<!-- Font Families -->
FontPrimary → EYInterstate, Segoe UI, Arial

<!-- Font Sizes -->
FontSizeTitle → 18
FontSizeSection → 16
FontSizeBody → 14
FontSizeCaption → 12
FontSizeButton → 12

<!-- Font Weights -->
FontWeightRegular → Normal
FontWeightMedium → Medium
FontWeightSemiBold → SemiBold
FontWeightBold → Bold
```

---

## Control Styles

### Card Container

Creates a styled border with background, corner radius, and shadow.

```xml
<Border Classes="Card" Padding="{StaticResource CardPaddingThickness}">
    <StackPanel Spacing="{DynamicResource SectionSpacing}">
        <TextBlock Classes="TitleMedium" Text="Card Title" />
        <TextBlock Text="Card content goes here..." />
    </StackPanel>
</Border>
```

### Standard Input Controls

All input controls have consistent sizing and styling:

```xml
<!-- Text Input -->
<TextBox Watermark="Enter engagement ID..." />

<!-- Dropdown -->
<ComboBox ItemsSource="{Binding Customers}"
          SelectedItem="{Binding SelectedCustomer}" />

<!-- Date Picker -->
<DatePicker SelectedDate="{Binding StartDate}" />

<!-- Numeric Input -->
<NumericUpDown Value="{Binding Amount}"
               FormatString="C2"
               Minimum="0" />
```

### Navigation Toggle Button

Used for sidebar/navigation menus:

```xml
<ToggleButton Classes="NavButton"
              IsChecked="{Binding IsExpanded}">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <Path Data="{StaticResource IconHome}" />
        <TextBlock Text="Home" />
    </StackPanel>
</ToggleButton>
```

### DataGrid Styling

DataGrid is automatically styled with proper colors, fonts, and row selection:

```xml
<DataGrid ItemsSource="{Binding Engagements}"
          SelectedItem="{Binding SelectedEngagement}"
          AutoGenerateColumns="False"
          HeadersVisibility="Column"
          GridLinesVisibility="Horizontal">
    <DataGrid.Columns>
        <DataGridTextColumn Header="ID" Binding="{Binding Id}" />
        <DataGridTextColumn Header="Description" Binding="{Binding Description}" />
    </DataGrid.Columns>
</DataGrid>
```

---

## Dialog Templates

### ConfirmationDialog

**Use Cases**: Yes/No confirmations, Save/Cancel prompts, Delete confirmations

#### Creating a Confirmation Dialog ViewModel

```csharp
using GRC.Shared.UI.ViewModels.Dialogs;

public partial class DeleteConfirmationViewModel : ConfirmationDialogViewModelBase
{
    public DeleteConfirmationViewModel(string itemName, Action onConfirmed)
    {
        Title = "Confirm Delete";
        Message = $"Are you sure you want to delete '{itemName}'? This action cannot be undone.";
        ConfirmButtonText = "Delete";
        CancelButtonText = "Cancel";
        OnConfirmed = onConfirmed;
    }
}
```

#### Showing the Dialog

```csharp
// In your ViewModel or Service
var viewModel = new DeleteConfirmationViewModel(
    engagement.Description,
    onConfirmed: async () => await DeleteEngagement(engagement.Id)
);

bool result = await dialogService.ShowDialogAsync(viewModel, "Confirm Delete");
```

#### Using the Control Directly in XAML

```xml
xmlns:dialogs="clr-namespace:GRC.Shared.UI.Controls.Dialogs;assembly=GRC.Shared.UI"

<dialogs:ConfirmationDialog DataContext="{Binding ConfirmationViewModel}" />
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Title` | string | Dialog title (optional, hidden if empty) |
| `Message` | string | Main message text |
| `CustomContent` | object | Additional content slot (optional) |
| `ConfirmButtonText` | string | Confirm button label (default: "OK") |
| `CancelButtonText` | string | Cancel button label (default: "Cancel") |
| `ConfirmCommand` | IRelayCommand | Executes OnConfirmed callback and closes |
| `CancelCommand` | IRelayCommand | Executes OnCanceled callback and closes |

---

### InformationDialog

**Use Cases**: Error messages, information notifications, warnings, success confirmations

#### Creating an Information Dialog ViewModel

```csharp
using Avalonia;
using Avalonia.Media;
using GRC.Shared.UI.ViewModels.Dialogs;

public partial class ErrorDialogViewModel : InformationDialogViewModelBase
{
    public ErrorDialogViewModel(string message, Exception? exception = null)
    {
        Title = "Error";
        Message = message;
        Details = exception?.ToString();
        DetailsHeaderText = "Technical Details";
        IconData = (Geometry)Application.Current.FindResource("IconError");
        DismissButtonText = "OK";
    }
}
```

#### Showing the Dialog

```csharp
try
{
    await ImportDataAsync();
}
catch (Exception ex)
{
    var viewModel = new ErrorDialogViewModel(
        "Failed to import management data. Please check the file format.",
        ex
    );
    await dialogService.ShowDialogAsync(viewModel, "Import Error");
}
```

#### Success Message Example

```csharp
public partial class SuccessDialogViewModel : InformationDialogViewModelBase
{
    public SuccessDialogViewModel(string message)
    {
        Title = "Success";
        Message = message;
        IconData = (Geometry)Application.Current.FindResource("IconCheck");
        DismissButtonText = "OK";
    }
}

// Usage
var viewModel = new SuccessDialogViewModel("Allocation saved successfully!");
await dialogService.ShowDialogAsync(viewModel, "Success");
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Title` | string | Dialog title (optional, centered) |
| `Message` | string | Main message text (centered, wrapped) |
| `IconData` | Geometry | Optional icon (48x48, centered above title) |
| `Details` | string | Collapsible technical details (optional) |
| `DetailsHeaderText` | string | Expander header (default: "Show Details") |
| `DismissButtonText` | string | Dismiss button label (default: "OK") |
| `DismissCommand` | IRelayCommand | Closes the dialog |

---

## Shared Controls & DataTemplates

- **Status/Feedback Controls:** `StatusBar`, `LoadingIndicator`, `EmptyState`, `ToastNotification`, and `SearchBox` live in `GRC.Shared.UI/Controls`. They rely on theme tokens for padding, borders, and icons. Toast overlays in both shells bind `ToastService.Notifications` into `ToastNotification` with `ToastBrushConverter` for severity.
- **Dialogs:** App dialog views now wrap `ConfirmationDialog` and `InformationDialog` from `GRC.Shared.UI.Controls.Dialogs`; base view models receive `CloseDialog` from `BaseDialogService` so confirm/dismiss actions close modals without manual callbacks.
- **DataTemplates:** `avares://GRC.Shared.UI/DataTemplates/DataTemplates.axaml` merges StandardListItem, DetailListItem, and GroupHeader templates; both apps include this dictionary in `App.axaml` for immediate reuse.
- **Usage snippets:**

```xml
<!-- Toast item template -->
<DataTemplate x:DataType="services:ToastNotification">
    <sharedControls:ToastNotification AccentBrush="{Binding Type, Converter={StaticResource ToastBrushConverter}}" />
</DataTemplate>

<!-- Search box -->
<sharedControls:SearchBox Text="{Binding Query, Mode=TwoWay}" Placeholder="Search engagements" />

<!-- Empty state / loading -->
<sharedControls:EmptyState Title="No data" Subtitle="Adjust filters" />
<sharedControls:LoadingIndicator Message="Loading data..." />

<!-- Common list template -->
<ListBox ItemTemplate="{StaticResource StandardListItemTemplate}" />
```

## Best Practices

### 1. Resource References

**Use StaticResource for theme tokens (better performance)**:
```xml
Background="{StaticResource BrushSurface}"
FontFamily="{StaticResource FontPrimary}"
FontSize="{StaticResource FontSizeBody}"
```

**Use DynamicResource for runtime-changeable values**:
```xml
Spacing="{DynamicResource GridGap}"
Padding="{DynamicResource CardPaddingThickness}"
```

### 2. Compiled Bindings

Always specify `x:DataType` for compiled bindings (better performance, compile-time checking):

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:vm="using:MyApp.ViewModels"
             x:DataType="vm:EngagementsViewModel">
    <TextBlock Text="{Binding SelectedEngagement.Description}" />
</UserControl>
```

### 3. Dialog ViewModel Pattern

**Step 1**: Create a derived ViewModel from the base:
```csharp
public partial class MyConfirmationViewModel : ConfirmationDialogViewModelBase
{
    public MyConfirmationViewModel(Action onConfirmed)
    {
        Title = "My Custom Title";
        Message = "My custom message";
        OnConfirmed = onConfirmed;
    }
}
```

**Step 2**: Show the dialog:
```csharp
var vm = new MyConfirmationViewModel(() => DoSomething());
bool result = await dialogService.ShowDialogAsync(vm);
```

### 4. Localization

For localized applications, set button text using your localization service:

```csharp
public partial class LocalizedConfirmationViewModel : ConfirmationDialogViewModelBase
{
    public LocalizedConfirmationViewModel(ILocalizationService localizer, Action onConfirmed)
    {
        Title = localizer.Get("Dialog_Confirm_Title");
        Message = localizer.Get("Dialog_Confirm_Message");
        ConfirmButtonText = localizer.Get("Global_Button_Save");
        CancelButtonText = localizer.Get("Global_Button_Cancel");
        OnConfirmed = onConfirmed;
    }
}
```

### 5. Custom Dialog Content

Use `CustomContent` for additional UI in ConfirmationDialog:

```csharp
var viewModel = new ConfirmationDialogViewModelBase
{
    Title = "Export Options",
    Message = "Configure export settings:",
    CustomContent = new ExportOptionsControl(), // Your custom UserControl
    ConfirmButtonText = "Export",
    OnConfirmed = () => ExportData()
};
```

### 6. Icon Usage

Use icons from the shared Icons.axaml resource dictionary:

```xml
<!-- In your AXAML -->
<Path Data="{StaticResource IconError}"
      Fill="{StaticResource BrushError}"
      Width="24" Height="24" />
```

```csharp
// In your C# code
IconData = (Geometry)Application.Current.FindResource("IconError");
```

---

## 🛠️ Extending the Component Library

### Adding a New Dialog Template

1. Create the AXAML control in `GRC.Shared.UI/Controls/Dialogs/`
2. Create the code-behind `.cs` file
3. Create a base ViewModel in `GRC.Shared.UI/ViewModels/Dialogs/`
4. Follow the existing patterns (ObservableObject, RelayCommand, CloseDialog callback)
5. Add documentation to this guide

### Adding a New Style

1. Determine the appropriate file:
   - Text styles → TextStyles.axaml
   - Control styles → Controls.axaml
   - Resources (colors, sizes) → Respective resource file
2. Add the style using proper naming conventions
3. Test in both GRCFinancialControl and InvoicePlanner applications
4. Document in this guide

---

## 📞 Questions or Issues?

- Check [AVALONIA_GUI_ASSESSMENT.md](AVALONIA_GUI_ASSESSMENT.md) for architectural overview
- Review [AVALONIA_GUI_SESSION3_SUMMARY.md](AVALONIA_GUI_SESSION3_SUMMARY.md) for implementation details
- Consult [AGENTS.md](AGENTS.md) for coding standards and governance

---

**Document Version**: 1.0  
**Last Updated**: Session 3  
**Maintained By**: GRC Development Team
