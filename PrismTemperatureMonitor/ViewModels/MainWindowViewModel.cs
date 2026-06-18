using System.Windows.Input;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using PrismTemperatureMonitor.Infrastructure;

namespace PrismTemperatureMonitor.ViewModels;

public sealed class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private string _currentPageTitle = "温度监控";

    public MainWindowViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
    }
    public ICommand NavigateCommand
    {
        get => new DelegateCommand<string>(Navigate);
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    private void Navigate(string? pageKey)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
        {
            return;
        }

        _regionManager.RequestNavigate(RegionNames.MainContentRegion, pageKey);
        CurrentPageTitle = pageKey switch
        {
            PageKeys.Recipe => "配方管理",
            PageKeys.LaserParameters => "激光器参数",
            _ => "温度监控"
        };
    }
}
