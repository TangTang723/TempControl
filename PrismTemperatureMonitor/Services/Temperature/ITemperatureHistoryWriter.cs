using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface ITemperatureHistoryWriter
{
    void Enqueue(TemperatureSample sample);
}
