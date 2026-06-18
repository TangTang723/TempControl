using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class TemperatureHistoryWriterTests
{
    [Fact]
    public async Task FlushAsync_WritesSamplesToDailyCsvFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "PrismTemperatureMonitorTests", Guid.NewGuid().ToString("N"));
        var writer = new TemperatureHistoryWriter(root);
        var sampleTime = new DateTime(2026, 6, 3, 14, 30, 15, DateTimeKind.Local);

        writer.Enqueue(new TemperatureSample(sampleTime, 7, 88.6));
        await writer.FlushAsync();

        var csvPath = Path.Combine(root, "2026-06-03.csv");
        Assert.True(File.Exists(csvPath));

        var lines = await File.ReadAllLinesAsync(csvPath);
        Assert.Equal("Timestamp,Index,Value", lines[0]);
        Assert.Equal("2026-06-03 14:30:15.000,7,88.6", lines[1]);

        await writer.DisposeAsync();
    }
}
