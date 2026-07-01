namespace PrismTemperatureMonitor.Models;

public sealed class MesRecipeRecord
{
    public DateTime Timestamp { get; set; }

    public double YAbsolutePositionSpeed { get; set; }

    public double ZAbsolutePositionSpeed { get; set; }

    public int WeldPassCount { get; set; }

    public List<MesWeldPassRecord> WeldPasses { get; set; } = [];
}

public sealed class MesWeldPassRecord
{
    public int Index { get; set; }

    public int ActualPower { get; set; }

    public int WaveNumber { get; set; }

    public double RSpeed { get; set; }

    public double YPosition { get; set; }

    public double ZPosition { get; set; }

    public double RPreAngle { get; set; }

    public double RPosition { get; set; }

    public double RPostAngle { get; set; }

    public double TemperatureUpperLimit { get; set; }

    public double TemperatureLowerLimit { get; set; }

    public double LaserPowerUpperLimit { get; set; }

    public double LaserPowerLowerLimit { get; set; }
}
