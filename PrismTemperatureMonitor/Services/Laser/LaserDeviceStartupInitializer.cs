namespace PrismTemperatureMonitor.Services;

public sealed class LaserDeviceStartupInitializer
{
    private readonly ILaserDeviceService _laserDeviceService;
    private readonly ILaserRealtimePollingService _realtimePollingService;
    private int _isInitialized;

    public LaserDeviceStartupInitializer(
        ILaserDeviceService laserDeviceService,
        ILaserRealtimePollingService realtimePollingService)
    {
        _laserDeviceService = laserDeviceService;
        _realtimePollingService = realtimePollingService;
    }

    public void Initialize()
    {
        if (Interlocked.Exchange(ref _isInitialized, 1) != 0)
        {
            return;
        }

        _laserDeviceService.ConnectAllAndReadWaveSettings();
        _realtimePollingService.Start();
    }
}
