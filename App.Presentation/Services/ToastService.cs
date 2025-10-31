using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Threading;
using App.Presentation.Localization;

namespace App.Presentation.Services;

public enum ToastType
{
    Success,
    Warning,
    Error
}

public sealed class ToastNotification
{
    internal ToastNotification(ToastType type, string message)
    {
        Id = Guid.NewGuid();
        Type = type;
        Message = message;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public ToastType Type { get; }

    public string Message { get; }

    public DateTimeOffset CreatedAt { get; }
}

public static class ToastService
{
    private static readonly ObservableCollection<ToastNotification> NotificationsImpl = new();
    private static readonly ReadOnlyObservableCollection<ToastNotification> NotificationsReadOnly = new(NotificationsImpl);
    private static readonly Dictionary<Guid, DispatcherTimer> Timers = new();
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(3);

    public static ReadOnlyObservableCollection<ToastNotification> Notifications => NotificationsReadOnly;

    public static void ShowSuccess(string resourceKey, params object[] arguments)
    {
        Show(ToastType.Success, resourceKey, arguments);
    }

    public static void ShowWarning(string resourceKey, params object[] arguments)
    {
        Show(ToastType.Warning, resourceKey, arguments);
    }

    public static void ShowError(string resourceKey, params object[] arguments)
    {
        Show(ToastType.Error, resourceKey, arguments);
    }

    public static void Dismiss(Guid id)
    {
        void Execute()
        {
            if (Timers.Remove(id, out var timer))
            {
                timer.Stop();
            }

            for (var index = NotificationsImpl.Count - 1; index >= 0; index--)
            {
                if (NotificationsImpl[index].Id != id)
                {
                    continue;
                }

                NotificationsImpl.RemoveAt(index);
                break;
            }
        }

        Dispatch(Execute);
    }

    private static void Show(ToastType type, string resourceKey, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return;
        }

        var message = arguments is { Length: > 0 }
            ? LocalizationRegistry.Format(resourceKey, arguments)
            : LocalizationRegistry.Get(resourceKey);

        void Execute()
        {
            var notification = new ToastNotification(type, message);
            NotificationsImpl.Add(notification);
            ScheduleDismiss(notification.Id, DefaultDuration);
        }

        Dispatch(Execute);
    }

    private static void ScheduleDismiss(Guid id, TimeSpan interval)
    {
        if (Timers.TryGetValue(id, out var existingTimer))
        {
            existingTimer.Stop();
        }

        var timer = new DispatcherTimer
        {
            Interval = interval
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Timers.Remove(id);
            RemoveNotification(id);
        };

        Timers[id] = timer;
        timer.Start();
    }

    private static void RemoveNotification(Guid id)
    {
        for (var index = NotificationsImpl.Count - 1; index >= 0; index--)
        {
            if (NotificationsImpl[index].Id != id)
            {
                continue;
            }

            NotificationsImpl.RemoveAt(index);
            break;
        }
    }

    private static void Dispatch(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
