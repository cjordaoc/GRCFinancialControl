using App.Presentation.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace InvoicePlanner.Avalonia.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public HomeViewModel(
        PlanEditorViewModel planEditor,
        RequestConfirmationViewModel requestConfirmation,
        EmissionConfirmationViewModel emissionConfirmation,
        InvoiceSummaryViewModel summary,
        NotificationPreviewViewModel notificationPreview,
        IMessenger messenger)
        : base(messenger)
    {
        PlanEditor = planEditor;
        RequestConfirmation = requestConfirmation;
        EmissionConfirmation = emissionConfirmation;
        Summary = summary;
        NotificationPreview = notificationPreview;
        welcomeMessage = LocalizationRegistry.Get("INV_Home_Message_Welcome");
    }

    [ObservableProperty]
    private string welcomeMessage;

    public PlanEditorViewModel PlanEditor { get; }

    public RequestConfirmationViewModel RequestConfirmation { get; }

    public EmissionConfirmationViewModel EmissionConfirmation { get; }

    public InvoiceSummaryViewModel Summary { get; }

    public NotificationPreviewViewModel NotificationPreview { get; }
}
