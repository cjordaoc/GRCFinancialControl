using System;

namespace App.Presentation.Controls;

public class ModalOverlayCloseRequestedEventArgs : EventArgs
{
    public ModalOverlayCloseRequestedEventArgs(bool result)
    {
        Result = result;
    }

    public bool Result { get; }
}
