using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface IRecipeConfigStore
{
    RecipeSettings Load();

    void Save(RecipeSettings settings);
}
