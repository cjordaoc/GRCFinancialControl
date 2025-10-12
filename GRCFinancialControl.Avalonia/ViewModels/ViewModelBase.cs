using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GRCFinancialControl.Avalonia.ViewModels
{
    public class ViewModelBase : ObservableObject
    {
        public virtual Task LoadDataAsync()
        {
            return Task.CompletedTask;
        }
    }
}
