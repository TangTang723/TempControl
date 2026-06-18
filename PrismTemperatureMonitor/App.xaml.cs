using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using PrismTemperatureMonitor.Infrastructure;
using PrismTemperatureMonitor.Services;
using PrismTemperatureMonitor.ViewModels;
using PrismTemperatureMonitor.Views;

namespace PrismTemperatureMonitor;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void ConfigureViewModelLocator()
    {
        base.ConfigureViewModelLocator();
        ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ITemperatureHistoryWriter, TemperatureHistoryWriter>();
        containerRegistry.RegisterInstance<IPlcService>(PlcService.Instance);
        containerRegistry.RegisterSingleton<IRecipeConfigStore, RecipeConfigStore>();
        containerRegistry.RegisterSingleton<ILaserDeviceConfigStore, LaserDeviceConfigStore>();
        containerRegistry.RegisterSingleton<ILaserSerialPortFactory, SystemLaserSerialPortFactory>();
        containerRegistry.RegisterSingleton<ILaserDeviceService, LaserDeviceService>();
        containerRegistry.RegisterSingleton<ILaserRealtimePollingService, LaserRealtimePollingService>();
        containerRegistry.RegisterSingleton<LaserDeviceStartupInitializer>();
        containerRegistry.RegisterForNavigation<TemperatureMonitorView>(PageKeys.TemperatureMonitor);
        containerRegistry.RegisterForNavigation<RecipeView>(PageKeys.Recipe);
        containerRegistry.RegisterForNavigation<LaserParametersView>(PageKeys.LaserParameters);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Container.Resolve<LaserDeviceStartupInitializer>().Initialize();
        Container.Resolve<IRegionManager>()
            .RequestNavigate(RegionNames.MainContentRegion, PageKeys.TemperatureMonitor);
    }
}
