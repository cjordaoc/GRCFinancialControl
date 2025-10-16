using CommunityToolkit.Mvvm.ComponentModel;
using InvoicePlanner.Avalonia.Resources;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public HomeViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation,
        InvoiceSummaryViewModel summary,
        NotificationPreviewViewModel notificationPreview)
    {
        PlanEditor = planEditor;
        RequestConfirmation = requestConfirmation;
        EmissionConfirmation = emissionConfirmation;
        Summary = summary;
        NotificationPreview = notificationPreview;
        welcomeMessage = Strings.Get("HomeWelcome");
    }

    [ObservableProperty]
    private string welcomeMessage;

    public PlanEditorViewModel PlanEditor { get; }

    public RequestConfirmationViewModel RequestConfirmation { get; }

    public EmissionConfirmationViewModel EmissionConfirmation { get; }

    public InvoiceSummaryViewModel Summary { get; }

    public NotificationPreviewViewModel NotificationPreview { get; }
}
