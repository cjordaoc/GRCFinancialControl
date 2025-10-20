using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace GRCFinancialControl.Avalonia.Views.Dialogs
{
    public partial class DialogWindow : Window
    {
        private IDisposable? _ownerSizeSubscription;
        private Border? _dialogContainer;

        public DialogWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _dialogContainer = this.FindControl<Border>("DialogContainer");
            ApplyThemeResources();
        }

        private void ApplyThemeResources()
        {
            if (_dialogContainer is null)
            {
                return;
            }

            var defaultShadow = BoxShadows.Parse("0 4 24 0 #66000000");

            if (!this.TryFindResource("ModalDialogShadow", out var shadowResource))
            {
                _dialogContainer.BoxShadow = defaultShadow;
                return;
            }

            _dialogContainer.BoxShadow = shadowResource switch
            {
                BoxShadows boxShadows => boxShadows,
                string shadowString => BoxShadows.Parse(shadowString),
                _ => defaultShadow
            };
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (Owner is Window owner)
            {
                UpdateSizing(owner.ClientSize);
                _ownerSizeSubscription = owner.GetObservable(Window.ClientSizeProperty)
                    .Subscribe(UpdateSizing);
                Position = owner.Position;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _ownerSizeSubscription?.Dispose();
            _ownerSizeSubscription = null;
        }

        private void UpdateSizing(Size size)
        {
            if (_dialogContainer is null)
            {
                return;
            }

            var ownerWidth = size.Width > 0 ? size.Width : double.NaN;
            var ownerHeight = size.Height > 0 ? size.Height : double.NaN;

            if (Owner is Window owner)
            {
                if (!(ownerWidth > 0))
                {
                    ownerWidth = owner.Bounds.Width;
                }

                if (!(ownerHeight > 0))
                {
                    ownerHeight = owner.Bounds.Height;
                }

                Position = owner.Position;
            }

            _dialogContainer.MaxWidth = ownerWidth > 0 ? ownerWidth * 0.85 : double.PositiveInfinity;
            _dialogContainer.MaxHeight = ownerHeight > 0 ? ownerHeight * 0.85 : double.PositiveInfinity;

            if (ownerWidth > 0)
            {
                Width = ownerWidth;
            }

            if (ownerHeight > 0)
            {
                Height = ownerHeight;
            }
        }
    }
}
