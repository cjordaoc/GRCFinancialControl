using System.Windows.Input;

namespace App.Presentation.Controls;

public interface IModalOverlayActionProvider
{
    bool IsPrimaryActionVisible { get; }

    string? PrimaryActionText { get; }

    ICommand? PrimaryActionCommand { get; }
}
