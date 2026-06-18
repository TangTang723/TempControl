using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public sealed class HwdLaserSystemSettings : BindableBase
{
    private bool _waveExternalEnabled;
    private bool _laserTriggerExternalEnabled;

    public bool WaveExternalEnabled
    {
        get => _waveExternalEnabled;
        set => SetProperty(ref _waveExternalEnabled, value);
    }

    public bool LaserTriggerExternalEnabled
    {
        get => _laserTriggerExternalEnabled;
        set => SetProperty(ref _laserTriggerExternalEnabled, value);
    }

    public HwdLaserSystemSettings Clone()
    {
        return new HwdLaserSystemSettings
        {
            WaveExternalEnabled = WaveExternalEnabled,
            LaserTriggerExternalEnabled = LaserTriggerExternalEnabled
        };
    }
}
