# Toast Service Refactoring Script
# Converts: ToastService.ShowX("Key", args) 
# To: var message = LocalizationRegistry.Format("Key", args); ToastService.ShowX(message);

$files = @(
    "GRCFinancialControl.Avalonia\ViewModels\ClosingPeriodEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\ClosingPeriodsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\EngagementEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\EngagementsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\FiscalYearEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\FiscalYearsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\HomeViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\ImportViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\ManagerEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\ManagersViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\PapdEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\PapdViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\RankMappingEditorViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\RankMappingsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\RevenueAllocationsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\SettingsViewModel.cs",
    "GRCFinancialControl.Avalonia\ViewModels\TasksViewModel.cs",
    "InvoicePlanner.Avalonia\ViewModels\EmissionConfirmationViewModel.cs",
    "InvoicePlanner.Avalonia\ViewModels\PlanEditorViewModel.cs",
    "InvoicePlanner.Avalonia\ViewModels\RequestConfirmationViewModel.cs"
)

$updatedCount = 0

foreach ($file in $files) {
    $path = "c:\Users\caio.calisto\OneDrive - EY\Documentos\GitHub\GRCFinancialControl\$file"
    
    if (-not (Test-Path $path)) {
        Write-Host " Not found: $file" -ForegroundColor Red
        continue
    }
    
    $content = Get-Content $path -Raw
    $original = $content
    
    # Replace App.Presentation.Services with GRC.Shared.UI.Services for ToastService
    if ($content -match "using App\.Presentation\.Services;") {
        if ($content -notmatch "using GRC\.Shared\.UI\.Services;") {
            $content = $content -replace "(using App\.Presentation\.Localization;)", "`$1`nusing GRC.Shared.UI.Services;"
        }
    }
    
    # Pattern 1: ToastService.ShowX("Key", arg1, arg2, ...) with arguments
    $content = $content -replace '(\s+)ToastService\.(ShowSuccess|ShowWarning|ShowError)\s*\(\s*"([^"]+)"([^)]+)\);', '$1var message = LocalizationRegistry.Format("$3"$4);$1ToastService.$2(message);'
    
    # Pattern 2: ToastService.ShowX("Key") without arguments
    $content = $content -replace '(\s+)ToastService\.(ShowSuccess|ShowWarning|ShowError)\s*\(\s*"([^"]+)"\s*\);', '$1var message = LocalizationRegistry.Get("$3");$1ToastService.$2(message);'
    
    if ($content -ne $original) {
        Set-Content $path $content -NoNewline
        Write-Host " Updated: $file" -ForegroundColor Green
        $updatedCount++
    } else {
        Write-Host " No changes: $file" -ForegroundColor Gray
    }
}

Write-Host "`nUpdated $updatedCount files" -ForegroundColor Cyan
