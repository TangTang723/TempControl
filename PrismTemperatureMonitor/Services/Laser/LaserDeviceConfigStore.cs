using System.IO;
using System.Text.Json;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class LaserDeviceConfigStore : ILaserDeviceConfigStore
{
    private readonly string _filePath;

    public LaserDeviceConfigStore()
        : this(Path.Combine(AppContext.BaseDirectory, "Config", "LaserDevices.json"))
    {
    }

    public LaserDeviceConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public IReadOnlyList<LaserDeviceConfig> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<LaserDeviceConfig>>(json) ?? [];
    }

    public void Save(IEnumerable<LaserDeviceConfig> devices)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(devices, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
