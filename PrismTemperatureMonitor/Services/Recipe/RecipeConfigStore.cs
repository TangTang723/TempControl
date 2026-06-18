using System.IO;
using System.Text.Json;
using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class RecipeConfigStore : IRecipeConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;

    public RecipeConfigStore()
        : this(Path.Combine(AppContext.BaseDirectory, "Config", "RecipeSettings.json"))
    {
    }

    public RecipeConfigStore(string filePath)
    {
        _filePath = filePath;
    }

    public RecipeSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new RecipeSettings();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<RecipeSettings>(json, JsonOptions) ?? new RecipeSettings();
    }

    public void Save(RecipeSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = _filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
