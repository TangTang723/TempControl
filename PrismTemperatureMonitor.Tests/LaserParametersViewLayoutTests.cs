using System.Xml.Linq;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserParametersViewLayoutTests
{
    [Fact]
    public void HwdWaveCommandButtons_AreRightAligned()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewPath = Path.Combine(
            repositoryRoot,
            "PrismTemperatureMonitor",
            "Views",
            "LaserParametersView.xaml");
        var document = XDocument.Load(viewPath);
        var xamlNamespace = document.Root!.Name.Namespace;

        var commandBar = document
            .Descendants(xamlNamespace + "DockPanel")
            .Single(panel => panel
                .Elements(xamlNamespace + "TextBlock")
                .Any(text => (string?)text.Attribute("Text") == "HWD 波形基础参数"));
        var buttonPanel = commandBar.Elements(xamlNamespace + "StackPanel").Single();

        Assert.Equal("Right", (string?)buttonPanel.Attribute("HorizontalAlignment"));
    }

    [Fact]
    public void DeviceEditor_HidesAddressAndShowsPowerScaleOnlyForHwd()
    {
        var xaml = File.ReadAllText(GetLaserParametersViewPath());

        Assert.DoesNotContain("NewDeviceAddress", xaml);
        Assert.DoesNotContain("地址/精度", xaml);
        Assert.Contains("功率精度", xaml);
        Assert.Contains("NewHwdPowerScale", xaml);
        Assert.Contains("IsNewDeviceHwd", xaml);
    }

    [Fact]
    public void HwdFwcEditor_ContainsDedicatedFieldsAndModeVisibilityBindings()
    {
        var xaml = File.ReadAllText(GetLaserParametersViewPath());

        Assert.Contains("CurrentHwdWaveSettings.FcwRampUpTime", xaml);
        Assert.Contains("CurrentHwdWaveSettings.FcwContinuousPower", xaml);
        Assert.Contains("CurrentHwdWaveSettings.FcwRampDownTime", xaml);
        Assert.Contains("Visibility=\"{Binding IsHwdFcwMode", xaml);
        Assert.Contains("Visibility=\"{Binding ShowWaveSegmentEditor", xaml);
    }

    [Fact]
    public void HwdParameterInformation_ContainsRealtimeAndWaveSnapshotFields()
    {
        var xaml = File.ReadAllText(GetLaserParametersViewPath());

        Assert.Contains("DisplayHwdPulseInterval", xaml);
        Assert.Contains("DisplayRealtimeLaserPower", xaml);
        Assert.Contains("DisplayOutputPointCount", xaml);
        Assert.Contains("DisplayOutputStatusBrush", xaml);
        Assert.Contains("DisplayAlarmStatusBrush", xaml);
        Assert.DoesNotContain("Text=\"出光状态:\"", xaml);
        Assert.DoesNotContain("Text=\"报警状态:\"", xaml);
        Assert.DoesNotContain("DisplayHwdModulationPeriod", xaml);
        Assert.DoesNotContain("DisplayHwdOutputRatio", xaml);
        Assert.DoesNotContain("DisplayHwdCurrentOutputWaveNumber", xaml);
    }

    [Fact]
    public void HwdParameterInformation_RealtimePowerSpansTwoColumnsAndPointCountUsesOneColumn()
    {
        var document = XDocument.Load(GetLaserParametersViewPath());
        var xamlNamespace = document.Root!.Name.Namespace;
        var realtimePowerLabel = document
            .Descendants(xamlNamespace + "TextBlock")
            .Single(text => ((string?)text.Attribute("Text"))?.Contains("实时功率(W):") == true);
        var realtimePowerItem = realtimePowerLabel.Ancestors(xamlNamespace + "Border").First();
        var parameterPanel = realtimePowerItem.Parent!;
        var pointCountItem = parameterPanel
            .Elements(xamlNamespace + "Border")
            .Single(border => border
                .Descendants(xamlNamespace + "TextBlock")
                .Any(text => ((string?)text.Attribute("Text"))?.Contains("出光点数:") == true));

        Assert.Equal("2", (string?)realtimePowerItem.Attribute("Grid.ColumnSpan"));
        Assert.Null(pointCountItem.Attribute("Grid.ColumnSpan"));
    }

    [Fact]
    public void HwdWaveBasicParameters_ShowReadOnlyPowerResultsWithoutCurrentOutputWave()
    {
        var document = XDocument.Load(GetLaserParametersViewPath());
        var xamlNamespace = document.Root!.Name.Namespace;
        var hwdPanel = document
            .Descendants(xamlNamespace + "Border")
            .Single(border => border
                .Descendants(xamlNamespace + "TextBlock")
                .Any(text => (string?)text.Attribute("Text") == "HWD 波形基础参数"));
        var panelXaml = hwdPanel.ToString();

        Assert.Contains("CurrentHwdWaveSettings.AveragePower", panelXaml);
        Assert.Contains("CurrentHwdWaveSettings.SinglePointEnergy", panelXaml);
        Assert.DoesNotContain("当前输出波形", panelXaml);
        Assert.DoesNotContain("CurrentHwdWaveSettings.CurrentOutputWaveNumber", panelXaml);
    }

    [Fact]
    public void HwdFcwBasicParameters_IncludePulseInterval()
    {
        var document = XDocument.Load(GetLaserParametersViewPath());
        var xamlNamespace = document.Root!.Name.Namespace;
        var hwdPanel = document
            .Descendants(xamlNamespace + "Border")
            .Single(border => border
                .Descendants(xamlNamespace + "TextBlock")
                .Any(text => (string?)text.Attribute("Text") == "HWD 波形基础参数"));
        var fcwPanels = hwdPanel
            .Descendants(xamlNamespace + "Grid")
            .Where(grid => ((string?)grid.Attribute("Visibility"))?.Contains("IsHwdFcwMode") == true);

        Assert.Contains(
            fcwPanels.SelectMany(panel => panel.Descendants(xamlNamespace + "TextBox")),
            textBox => ((string?)textBox.Attribute("Text"))?
                .Contains("CurrentHwdWaveSettings.PulseInterval") == true);
    }

    private static string GetLaserParametersViewPath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "PrismTemperatureMonitor",
            "Views",
            "LaserParametersView.xaml");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PrismTemperatureMonitor.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("找不到解决方案根目录。");
    }
}
