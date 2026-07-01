using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface IMesRecipeRecordWriter
{
    void Append(MesRecipeRecord record);
}
