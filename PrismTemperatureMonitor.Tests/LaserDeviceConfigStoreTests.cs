using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserDeviceConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsLaserDeviceConfigurations()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"LaserDevices-{Guid.NewGuid():N}.json");
        var store = new LaserDeviceConfigStore(filePath);
        var device = new LaserDeviceConfig
        {
            Id = Guid.NewGuid(),
            Name = "激光器A",
            PortName = "COM7",
            BaudRate = 115200,
            IsEnabled = true
        };

        store.Save([device]);
        var loaded = store.Load();

        var loadedDevice = Assert.Single(loaded);
        Assert.Equal(device.Id, loadedDevice.Id);
        Assert.Equal("激光器A", loadedDevice.Name);
        Assert.Equal("COM7", loadedDevice.PortName);
        Assert.Equal(115200, loadedDevice.BaudRate);
        Assert.True(loadedDevice.IsEnabled);
    }
}
