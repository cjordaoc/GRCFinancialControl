using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Markdown.Avalonia.Full;

namespace GRCFinancialControl.Avalonia.Controls;

public sealed class MarkdownPresenter : ScrollViewer
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownPresenter, string?>(nameof(Markdown));

    private readonly Markdown.Avalonia.Markdown _engine;

    static MarkdownPresenter()
    {
        MarkdownProperty.Changed.AddClassHandler<MarkdownPresenter>((presenter, change) =>
            presenter.OnMarkdownChanged(change.GetNewValue<string?>()));
    }

    public MarkdownPresenter()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        _engine = new Markdown.Avalonia.Markdown
        {
            UseResource = true,
            Plugins = new MdAvPlugins()
        };
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private void OnMarkdownChanged(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            Content = null;
            return;
        }

        Content = _engine.Transform(markdown);
    }
}
