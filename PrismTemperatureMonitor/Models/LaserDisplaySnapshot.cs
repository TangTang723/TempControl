namespace PrismTemperatureMonitor.Models;

public sealed class LaserDisplaySnapshot
{
    public Guid DeviceId { get; init; }

    public string DeviceName { get; init; } = string.Empty;

    public LaserDeviceModel Model { get; init; }

    public double RealtimePower { get; init; }

    public int WaveNumber { get; init; }

    public string WaveMode { get; init; } = string.Empty;

    public string TriggerMode { get; init; } = string.Empty;

    public double MaximumPower { get; init; }

    public double PulseInterval { get; init; }

    public double AveragePower { get; init; }

    public double SinglePointEnergy { get; init; }

    public int OutputPointCount { get; init; }

    public double AnalogVoltage { get; init; }
}
