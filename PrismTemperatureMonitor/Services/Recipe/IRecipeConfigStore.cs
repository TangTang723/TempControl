using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface IRecipeConfigStore
{
    event EventHandler<RecipeSettings>? SettingsSaved;

    RecipeSettings Load();

    void Save(RecipeSettings settings);
}
