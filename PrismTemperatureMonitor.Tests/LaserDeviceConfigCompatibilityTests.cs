using System.Text.Json;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserDeviceConfigCompatibilityTests
{
    [Fact]
    public void LegacyJsonWithoutModel_LoadsAsHwq()
    {
        const string json = """
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "name": "旧激光器",
              "portName": "COM3",
              "baudRate": 115200,
              "isEnabled": true
            }
            """;

        var config = JsonSerializer.Deserialize<LaserDeviceConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(config);
        Assert.Equal(LaserDeviceModel.HWQ, config.Model);
        Assert.Equal(0x01, config.DeviceAddress);
        Assert.Equal(HwdPowerScale.OneW, config.HwdPowerScale);
    }

    [Fact]
    public void LaserDeviceModel_UsesUppercaseDisplayNames()
    {
        Assert.Equal("HWQ", LaserDeviceModel.HWQ.ToString());
        Assert.Equal("HWD", LaserDeviceModel.HWD.ToString());
    }
}
