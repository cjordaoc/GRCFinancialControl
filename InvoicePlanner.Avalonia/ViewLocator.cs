using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using InvoicePlanner.Avalonia.ViewModels;

namespace InvoicePlanner.Avalonia;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "View not found" };
        }

        var name = data.GetType().FullName?.Replace("ViewModel", "View");
        if (name is not null)
        {
            var assemblyName = typeof(ViewLocator).Assembly.FullName;
            var type = Type.GetType($"{name}, {assemblyName}");
            if (type is not null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
        }

        return new TextBlock { Text = $"View not found: {name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
