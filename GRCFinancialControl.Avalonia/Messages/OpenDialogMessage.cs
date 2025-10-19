using CommunityToolkit.Mvvm.Messaging.Messages;
using GRCFinancialControl.Avalonia.ViewModels;

namespace GRCFinancialControl.Avalonia.Messages
{
    public class OpenDialogMessage : ValueChangedMessage<ViewModelBase>
    {
        public OpenDialogMessage(ViewModelBase viewModel, string? title = null, bool canClose = true)
            : base(viewModel)
        {
            Title = title;
            CanClose = canClose;
        }

        public ViewModelBase ViewModel => Value;

        public string? Title { get; }

        public bool CanClose { get; }
    }
}