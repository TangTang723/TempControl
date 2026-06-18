namespace PrismTemperatureMonitor.Models;

public sealed class RecipeSettings
{
    public string PlcIpAddress { get; set; } = string.Empty;

    public List<RecipeMetadata> Recipes { get; set; } = [];
}

public sealed class RecipeMetadata
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int StartByteOffset { get; set; }
}
