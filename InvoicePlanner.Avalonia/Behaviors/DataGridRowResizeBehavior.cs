using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace InvoicePlanner.Avalonia.Behaviors;

public class DataGridRowResizeBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DataGridRowResizeBehavior, DataGrid, bool>("IsEnabled");

    private const double ResizeThreshold = 6d;
    private const double MinRowHeight = 24d;

    private static readonly ConditionalWeakTable<DataGrid, ResizeState> States = new();
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeNorthSouth);

    static DataGridRowResizeBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<DataGrid>((grid, args) =>
        {
            if (args.NewValue is bool enabled)
            {
                OnIsEnabledChanged(grid, enabled);
            }
        });
    }

    public static void SetIsEnabled(DataGrid element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DataGrid element) => element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DataGrid grid, bool enabled)
    {
        if (enabled)
        {
            grid.PointerPressed += OnPointerPressed;
            grid.PointerReleased += OnPointerReleased;
            grid.PointerMoved += OnPointerMoved;
            grid.PointerCaptureLost += OnPointerCaptureLost;
        }
        else
        {
            grid.PointerPressed -= OnPointerPressed;
            grid.PointerReleased -= OnPointerReleased;
            grid.PointerMoved -= OnPointerMoved;
            grid.PointerCaptureLost -= OnPointerCaptureLost;

            if (States.TryGetValue(grid, out var state))
            {
                state.ResetCursor();
                States.Remove(grid);
            }
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (!TryGetRow(e, out var row))
        {
            return;
        }

        var position = e.GetPosition(row);
        if (row.Bounds.Height - position.Y > ResizeThreshold)
        {
            return;
        }

        var state = GetState(grid);
        state.BeginResize(row, position, GetCurrentHeight(row));

        e.Pointer.Capture(grid);
        e.Handled = true;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var state = GetState(grid);

        if (state.IsResizing && state.Row is { } resizingRow)
        {
            var position = e.GetPosition(resizingRow);
            var delta = position.Y - state.StartPosition.Y;
            var newHeight = Math.Max(MinRowHeight, state.OriginalHeight + delta);
            resizingRow.Height = newHeight;
            e.Handled = true;
            return;
        }

        if (TryGetRow(e, out var hoverRow))
        {
            state.UpdateHoverRow(hoverRow);
            var position = e.GetPosition(hoverRow);
            if (hoverRow.Bounds.Height - position.Y <= ResizeThreshold)
            {
                hoverRow.Cursor = ResizeCursor;
            }
            else
            {
                hoverRow.ClearValue(InputElement.CursorProperty);
            }
        }
        else
        {
            state.ClearHoverRow();
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is DataGrid grid && States.TryGetValue(grid, out var state))
        {
            if (state.IsResizing)
            {
                e.Pointer.Capture(null);
                state.EndResize();
                e.Handled = true;
            }
        }
    }

    private static void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (sender is DataGrid grid && States.TryGetValue(grid, out var state))
        {
            state.EndResize();
        }
    }

    private static bool TryGetRow(PointerEventArgs e, [NotNullWhen(true)] out DataGridRow? row)
    {
        row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>();
        return row is not null;
    }

    private static double GetCurrentHeight(DataGridRow row)
    {
        var height = row.Bounds.Height;

        if (double.IsNaN(height) || height <= 0)
        {
            height = row.Height;
        }

        if (double.IsNaN(height) || height <= 0)
        {
            height = MinRowHeight;
        }

        return Math.Max(MinRowHeight, height);
    }

    private sealed class ResizeState
    {
        public DataGridRow? Row { get; private set; }
        public DataGridRow? HoverRow { get; private set; }
        public Point StartPosition { get; private set; }
        public double OriginalHeight { get; private set; }
        public bool IsResizing { get; private set; }

        public void BeginResize(DataGridRow row, Point startPosition, double originalHeight)
        {
            Row = row;
            StartPosition = startPosition;
            OriginalHeight = originalHeight;
            IsResizing = true;
            row.Cursor = ResizeCursor;
        }

        public void EndResize()
        {
            if (Row is not null)
            {
                Row.Cursor = null;
                Row = null;
            }

            IsResizing = false;
        }

        public void UpdateHoverRow(DataGridRow row)
        {
            if (!ReferenceEquals(HoverRow, row))
            {
                HoverRow?.ClearValue(InputElement.CursorProperty);
                HoverRow = row;
            }
        }

        public void ClearHoverRow()
        {
            HoverRow?.ClearValue(InputElement.CursorProperty);
            HoverRow = null;
        }

        public void ResetCursor()
        {
            ClearHoverRow();
            if (Row is not null)
            {
                Row.ClearValue(InputElement.CursorProperty);
                Row = null;
            }
            IsResizing = false;
        }
    }

    private static ResizeState GetState(DataGrid grid) => States.GetValue(grid, _ => new ResizeState());
}
