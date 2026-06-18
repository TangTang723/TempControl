namespace PrismTemperatureMonitor.Models;

[Flags]
public enum LaserDeviceCapabilities
{
    None = 0,
    CommonControl = 1,
    RealtimeStatus = 2,
    HwqWaveSettings = 4,
    HwqSystemSettings = 8,
    HwdWaveSettings = 16,
    HwdSystemSettings = 32
}
