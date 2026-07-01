using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class MesRecipeRecordWriterTests
{
    [Fact]
    public void Append_WritesDailyCsvWithTimestampAndWeldPassColumns()
    {
        var root = Path.Combine(Path.GetTempPath(), $"MesRecipe-{Guid.NewGuid():N}");
        var filePath = Path.Combine(root, "2026-07-01.csv");
        try
        {
            var writer = new MesRecipeRecordWriter(root);
            var record = new MesRecipeRecord
            {
                Timestamp = new DateTime(2026, 7, 1, 8, 9, 10, 123),
                YAbsolutePositionSpeed = 130.5,
                ZAbsolutePositionSpeed = 134.5,
                WeldPassCount = 6,
                WeldPasses =
                [
                    new MesWeldPassRecord
                    {
                        Index = 1,
                        ActualPower = 1001,
                        WaveNumber = 2,
                        RSpeed = 142.5,
                        LaserPowerLowerLimit = 178.5
                    }
                ]
            };

            writer.Append(record);

            var lines = File.ReadAllLines(filePath);
            Assert.Equal(2, lines.Length);
            Assert.Contains("记录时间", lines[0]);
            Assert.Contains("焊道1实际功率", lines[0]);
            Assert.Contains("焊道1激光功率下限", lines[0]);
            Assert.StartsWith("2026-07-01 08:09:10.123,130.5,134.5,6,1001,2,142.5", lines[1]);
            Assert.Contains(",178.5,", lines[1]);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
