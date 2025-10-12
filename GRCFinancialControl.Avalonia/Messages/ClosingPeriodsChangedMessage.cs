using CommunityToolkit.Mvvm.Messaging.Messages;

namespace GRCFinancialControl.Avalonia.Messages
{
    public class ClosingPeriodsChangedMessage : ValueChangedMessage<bool>
    {
        public ClosingPeriodsChangedMessage() : base(true)
        {
        }
    }
}
