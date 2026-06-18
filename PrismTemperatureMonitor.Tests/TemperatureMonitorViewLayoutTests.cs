namespace PrismTemperatureMonitor.Tests;

public sealed class TemperatureMonitorViewLayoutTests
{
    [Fact]
    public void View_ContainsReadOnlyLaserSnapshotPanel()
    {
        var xaml = File.ReadAllText(GetViewPath());

        Assert.Contains("ItemsSource=\"{Binding ConnectedLaserDevices}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding SelectedLaserDevice", xaml);
        Assert.Contains("CurrentLaserSnapshot.RealtimePower", xaml);
        Assert.Contains("CurrentLaserSnapshot.AnalogVoltage", xaml);
        Assert.Contains("CurrentLaserSnapshot.MaximumPower", xaml);
        Assert.Contains("CurrentLaserSnapshot.PulseInterval", xaml);
        Assert.Contains("IsHwqLaserSelected", xaml);
        Assert.Contains("IsHwdLaserSelected", xaml);
    }

    [Fact]
    public void View_UsesCompactTemperatureGridAndWiderRightColumn()
    {
        var xaml = File.ReadAllText(GetViewPath());

        Assert.Contains("<ColumnDefinition Width=\"390\" />", xaml);
        Assert.Contains("Columns=\"2\" Rows=\"2\"", xaml);
    }

    private static string GetViewPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "PrismTemperatureMonitor",
            "Views",
            "TemperatureMonitorView.xaml"));
    }
}
