using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public sealed class LaserSystemSettings : BindableBase
{
    private int _laserTriggerModeIndex;
    private bool _redLightExternalEnabled;
    private bool _waveExternalEnabled;
    private bool _laserTriggerExternalEnabled;
    private bool _analogModulationEnabled;
    private bool _digitalModulationEnabled;

    public int LaserTriggerModeIndex
    {
        get => _laserTriggerModeIndex;
        set => SetProperty(ref _laserTriggerModeIndex, value);
    }

    public bool RedLightExternalEnabled
    {
        get => _redLightExternalEnabled;
        set => SetProperty(ref _redLightExternalEnabled, value);
    }

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

    public bool AnalogModulationEnabled
    {
        get => _analogModulationEnabled;
        set => SetProperty(ref _analogModulationEnabled, value);
    }

    public bool DigitalModulationEnabled
    {
        get => _digitalModulationEnabled;
        set => SetProperty(ref _digitalModulationEnabled, value);
    }

    public LaserSystemSettings Clone()
    {
        return new LaserSystemSettings
        {
            LaserTriggerModeIndex = LaserTriggerModeIndex,
            RedLightExternalEnabled = RedLightExternalEnabled,
            WaveExternalEnabled = WaveExternalEnabled,
            LaserTriggerExternalEnabled = LaserTriggerExternalEnabled,
            AnalogModulationEnabled = AnalogModulationEnabled,
            DigitalModulationEnabled = DigitalModulationEnabled
        };
    }
}
