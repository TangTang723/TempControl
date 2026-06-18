namespace PrismTemperatureMonitor.Models;

public sealed record TemperatureSample(DateTime Timestamp, int Index, double Value);
