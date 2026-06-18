using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class RecipeConfigStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsRecipeSettings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"RecipeSettings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new RecipeConfigStore(filePath);
            var settings = new RecipeSettings
            {
                PlcIpAddress = "192.168.1.25",
                Recipes =
                [
                    new RecipeMetadata
                    {
                        Code = "R001",
                        Name = "测试配方",
                        StartByteOffset = 128
                    }
                ]
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal("192.168.1.25", loaded.PlcIpAddress);
            var recipe = Assert.Single(loaded.Recipes);
            Assert.Equal("R001", recipe.Code);
            Assert.Equal("测试配方", recipe.Name);
            Assert.Equal(128, recipe.StartByteOffset);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var temporaryPath = filePath + ".tmp";
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    [Fact]
    public void Load_ReturnsEmptySettingsWhenFileDoesNotExist()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"MissingRecipeSettings-{Guid.NewGuid():N}.json");
        var store = new RecipeConfigStore(filePath);

        var loaded = store.Load();

        Assert.Equal(string.Empty, loaded.PlcIpAddress);
        Assert.Empty(loaded.Recipes);
    }
}
