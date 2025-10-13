using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;

namespace GRCFinancialControl.Avalonia.Messages
{
    public class SelectedReportRequestMessage : RequestMessage<IEnumerable<object>>
    {
    }
}