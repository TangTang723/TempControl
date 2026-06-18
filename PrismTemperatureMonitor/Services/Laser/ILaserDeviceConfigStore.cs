using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface ILaserDeviceConfigStore
{
    IReadOnlyList<LaserDeviceConfig> Load();

    void Save(IEnumerable<LaserDeviceConfig> devices);
}
