using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;
using PrismTemperatureMonitor.ViewModels;

namespace PrismTemperatureMonitor.Tests;

public sealed class TemperatureMonitorViewModelTests
{
    [Fact]
    public void NewViewModel_IsSingleZoneAndNotCollecting()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        Assert.False(viewModel.IsCollecting);
        Assert.Single(viewModel.Series);
        Assert.Equal("未采集", viewModel.CollectionState);
        Assert.Equal("正常", viewModel.AlarmState);
        Assert.Equal("等待采集", viewModel.TemperatureResult);
        Assert.Equal("时间", viewModel.XAxes[0].Name);
        Assert.Equal("温度 / °C", viewModel.YAxes[0].Name);
        Assert.Equal(0, viewModel.SampleCount);
        Assert.Equal(0, viewModel.RenderedPointCount);
        Assert.True(viewModel.XAxes[0].MinLimit > 0);
        Assert.True(viewModel.XAxes[0].MaxLimit > viewModel.XAxes[0].MinLimit);
    }

    [Fact]
    public void NewViewModel_ConnectsPlcOnStartup()
    {
        var plc = new RecordingTemperaturePlcService();

        _ = CreateViewModel(plcService: plc);

        Assert.Equal(1, plc.ConnectCount);
    }

    [Fact]
    public void AppendSample_WhileCollectingUsesTimestampForChartXValue()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        viewModel.StartCollectingCommand.Execute();
        viewModel.AppendSampleForTest();

        Assert.True(viewModel.IsCollecting);
        Assert.Equal("采集中", viewModel.CollectionState);
        Assert.Equal(1, viewModel.SampleCount);
        Assert.Equal(1, viewModel.RenderedPointCount);
        Assert.True(viewModel.CurrentTemperature > 0);
        Assert.Equal(viewModel.CurrentTemperature, viewModel.TemperatureUpperLimit);
        Assert.Equal(viewModel.CurrentTemperature, viewModel.TemperatureLowerLimit);
        Assert.Equal("温度正常", viewModel.TemperatureResult);
        Assert.Single(historyWriter.Samples);
        Assert.Equal(0, historyWriter.Samples[0].Index);
        Assert.Equal(viewModel.CurrentTemperature, historyWriter.Samples[0].Value);

        var firstPoint = Assert.Single(GetSeriesValues(viewModel));
        Assert.Equal(historyWriter.Samples[0].Timestamp.ToOADate(), firstPoint.X);
    }

    [Fact]
    public void AppendSample_WhenLongRunningKeepsRenderedPointsBoundedAndExpandsTimeAxis()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        viewModel.StartCollectingCommand.Execute();
        var initialMin = viewModel.XAxes[0].MinLimit;
        for (var i = 0; i < 100_000; i++)
        {
            viewModel.AppendSampleForTest();
        }

        Assert.Equal(100_000, viewModel.SampleCount);
        Assert.True(viewModel.RenderedPointCount <= TemperatureMonitorViewModel.MaxRenderedPointCount);
        Assert.Equal(100_000, historyWriter.Samples.Count);
        Assert.Equal(initialMin, viewModel.XAxes[0].MinLimit);
        Assert.True(viewModel.XAxes[0].MaxLimit >= historyWriter.Samples[^1].Timestamp.ToOADate());
    }

    [Fact]
    public void StartCollecting_AfterStopResetsSamplesAndAxisToDefaultTimeWindow()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        viewModel.StartCollectingCommand.Execute();
        for (var i = 0; i < 150; i++)
        {
            viewModel.AppendSampleForTest();
        }

        viewModel.StopCollectingCommand.Execute();
        var previousMin = viewModel.XAxes[0].MinLimit;
        viewModel.StartCollectingCommand.Execute();

        Assert.True(viewModel.IsCollecting);
        Assert.Equal(0, viewModel.SampleCount);
        Assert.Equal(0, viewModel.RenderedPointCount);
        Assert.True(viewModel.XAxes[0].MinLimit >= previousMin);
        Assert.True(viewModel.XAxes[0].MaxLimit > viewModel.XAxes[0].MinLimit);
        Assert.Equal("等待采集", viewModel.TemperatureResult);
    }

    [Fact]
    public void AppendSample_AfterStopDoesNotAddMorePoints()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        viewModel.StartCollectingCommand.Execute();
        viewModel.AppendSampleForTest();
        viewModel.StopCollectingCommand.Execute();
        viewModel.AppendSampleForTest();

        Assert.False(viewModel.IsCollecting);
        Assert.Equal("已停止", viewModel.CollectionState);
        Assert.Equal(1, viewModel.SampleCount);
        Assert.Equal(1, viewModel.RenderedPointCount);
        Assert.Single(historyWriter.Samples);
    }

    [Fact]
    public void ClearChart_RemovesSamplesAndResetsTemperatureSummary()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var viewModel = CreateViewModel(historyWriter);

        viewModel.StartCollectingCommand.Execute();
        viewModel.AppendSampleForTest();
        viewModel.ClearChartCommand.Execute();

        Assert.Equal(0, viewModel.SampleCount);
        Assert.Equal(0, viewModel.RenderedPointCount);
        Assert.Equal(0, viewModel.CurrentTemperature);
        Assert.Equal(0, viewModel.TemperatureUpperLimit);
        Assert.Equal(0, viewModel.TemperatureLowerLimit);
        Assert.Equal("等待采集", viewModel.TemperatureResult);
        Assert.True(viewModel.XAxes[0].MinLimit > 0);
        Assert.True(viewModel.XAxes[0].MaxLimit > viewModel.XAxes[0].MinLimit);
    }

    [Fact]
    public void LaserPanel_ListsOnlyConnectedDevicesAndSelectsFirst()
    {
        var laserService = new RecordingLaserCacheService();
        var plc = new RecordingTemperaturePlcService();
        var first = laserService.AddDevice("激光器 1", LaserDeviceModel.HWQ, isConnected: true);
        laserService.AddDevice("激光器 2", LaserDeviceModel.HWD, isConnected: false);

        var viewModel = CreateViewModel(plcService: plc, laserService: laserService);

        viewModel.RefreshLaserDevicesForTest();

        var selected = Assert.Single(viewModel.ConnectedLaserDevices);
        Assert.Equal(first.Id, selected.Id);
        Assert.Equal(first.Id, viewModel.SelectedLaserDevice?.Id);
    }

    [Fact]
    public void LaserPanel_RefreshesOnlySelectedDeviceCachedSnapshot()
    {
        var laserService = new RecordingLaserCacheService();
        var plc = new RecordingTemperaturePlcService();
        var first = laserService.AddDevice("激光器 1", LaserDeviceModel.HWQ, isConnected: true);
        var second = laserService.AddDevice("激光器 2", LaserDeviceModel.HWD, isConnected: true);
        laserService.Snapshots[second.Id] = new LaserDisplaySnapshot
        {
            DeviceId = second.Id,
            DeviceName = second.Name,
            Model = LaserDeviceModel.HWD,
            RealtimePower = 18.5,
            WaveNumber = 3
        };

        var viewModel = CreateViewModel(plcService: plc, laserService: laserService);
        viewModel.RefreshLaserDevicesForTest();
        laserService.SnapshotRequests.Clear();

        viewModel.SelectedLaserDevice = second;
        viewModel.RefreshLaserSnapshotForTest();

        Assert.All(laserService.SnapshotRequests, deviceId => Assert.Equal(second.Id, deviceId));
        Assert.DoesNotContain(first.Id, laserService.SnapshotRequests);
        Assert.Equal(18.5, viewModel.CurrentLaserSnapshot?.RealtimePower);
        var write = Assert.Single(plc.FloatWrites);
        Assert.Equal(TemperatureMonitorViewModel.PlcLaserRealtimePowerAddress, write.Address);
        Assert.Equal(18.5f, write.Value);
        Assert.Equal(0, laserService.CommunicationReadCount);
    }

    [Fact]
    public void LaserPanel_WhenSelectedDeviceDisconnectsSelectsNextConnectedDevice()
    {
        var laserService = new RecordingLaserCacheService();
        var first = laserService.AddDevice("激光器 1", LaserDeviceModel.HWQ, isConnected: true);
        var second = laserService.AddDevice("激光器 2", LaserDeviceModel.HWD, isConnected: true);
        var viewModel = CreateViewModel(laserService: laserService);
        viewModel.RefreshLaserDevicesForTest();
        Assert.Equal(first.Id, viewModel.SelectedLaserDevice?.Id);

        laserService.ConnectedDeviceIds.Remove(first.Id);
        viewModel.RefreshLaserDevicesForTest();

        Assert.Equal(second.Id, viewModel.SelectedLaserDevice?.Id);
    }

    [Fact]
    public void PlcStartSignalRisingEdge_ClearsCurveAndReadsTemperature()
    {
        var historyWriter = new RecordingTemperatureHistoryWriter();
        var plc = new RecordingTemperaturePlcService();
        var viewModel = CreateViewModel(historyWriter, plc);
        viewModel.StartCollectingCommand.Execute();
        viewModel.AppendSampleForTest();
        Assert.Equal(1, viewModel.SampleCount);

        plc.BoolValues[TemperatureMonitorViewModel.PlcAcquisitionStartAddress] = true;
        plc.FloatValues[TemperatureMonitorViewModel.PlcTemperatureAddress] = 88.6f;

        viewModel.PollPlcForTest();

        Assert.True(viewModel.IsCollecting);
        Assert.Equal(1, viewModel.SampleCount);
        Assert.Equal(88.6, viewModel.CurrentTemperature, 1);
        Assert.Equal(88.6, historyWriter.Samples.Last().Value, 1);
    }

    [Fact]
    public void PlcStartSignalFallingEdge_ReadsTemperatureResult()
    {
        var plc = new RecordingTemperaturePlcService();
        var viewModel = CreateViewModel(plcService: plc);

        plc.BoolValues[TemperatureMonitorViewModel.PlcAcquisitionStartAddress] = true;
        plc.FloatValues[TemperatureMonitorViewModel.PlcTemperatureAddress] = 90f;
        viewModel.PollPlcForTest();

        plc.BoolValues[TemperatureMonitorViewModel.PlcAcquisitionStartAddress] = false;
        plc.BoolValues[TemperatureMonitorViewModel.PlcTemperatureResultAddress] = false;
        viewModel.PollPlcForTest();

        Assert.False(viewModel.IsCollecting);
        Assert.Equal("NG", viewModel.TemperatureResult);
    }

    [Fact]
    public void SelectingRecipe_WritesSelectedIndexToPlc()
    {
        var plc = new RecordingTemperaturePlcService();
        var store = new RecordingRecipeConfigStore
        {
            LoadedSettings = new RecipeSettings
            {
                Recipes =
                [
                    new RecipeMetadata { Code = "R002", Name = "150H-B002", StartByteOffset = 64 }
                ]
            }
        };
        var viewModel = CreateViewModel(plcService: plc, recipeStore: store);

        viewModel.SelectedTemperatureRecipe = viewModel.TemperatureRecipes[1];

        Assert.Equal("150H-B002", viewModel.SelectedTemperatureRecipe?.Name);
        var write = Assert.Single(plc.IntWrites);
        Assert.Equal(TemperatureMonitorViewModel.PlcRecipeIndexAddress, write.Address);
        Assert.Equal(2, write.Value);
    }

    private static TemperatureMonitorViewModel CreateViewModel(
        ITemperatureHistoryWriter? historyWriter = null,
        IPlcService? plcService = null,
        IRecipeConfigStore? recipeStore = null,
        ILaserDeviceService? laserService = null)
    {
        return new TemperatureMonitorViewModel(
            historyWriter ?? new RecordingTemperatureHistoryWriter(),
            laserService ?? new RecordingLaserCacheService(),
            plcService ?? new RecordingTemperaturePlcService(),
            recipeStore ?? new RecordingRecipeConfigStore());
    }

    private static IReadOnlyList<LiveChartsCore.Defaults.ObservablePoint> GetSeriesValues(
        TemperatureMonitorViewModel viewModel)
    {
        var series = Assert.IsType<LiveChartsCore.SkiaSharpView.LineSeries<LiveChartsCore.Defaults.ObservablePoint>>(
            viewModel.Series[0]);
        Assert.NotNull(series.Values);
        return series.Values.ToList();
    }

    private sealed class RecordingTemperatureHistoryWriter : ITemperatureHistoryWriter
    {
        public List<TemperatureSample> Samples { get; } = [];

        public void Enqueue(TemperatureSample sample)
        {
            Samples.Add(sample);
        }
    }

    private sealed class RecordingLaserCacheService : ILaserDeviceService
    {
        private readonly List<LaserDeviceConfig> _devices = [];

        public HashSet<Guid> ConnectedDeviceIds { get; } = [];

        public Dictionary<Guid, LaserDisplaySnapshot> Snapshots { get; } = [];

        public List<Guid> SnapshotRequests { get; } = [];

        public int CommunicationReadCount { get; private set; }

        public LaserDeviceConfig AddDevice(
            string name,
            LaserDeviceModel model,
            bool isConnected)
        {
            var device = new LaserDeviceConfig
            {
                Name = name,
                Model = model,
                IsEnabled = true
            };
            _devices.Add(device);
            if (isConnected)
            {
                ConnectedDeviceIds.Add(device.Id);
            }

            return device;
        }

        public IReadOnlyList<LaserDeviceConfig> GetDevices() => _devices;
        public void SaveDevices(IEnumerable<LaserDeviceConfig> devices) { }
        public void AddOrUpdateDevice(LaserDeviceConfig device) { }
        public void RemoveDevice(Guid deviceId) { }
        public void Connect(Guid deviceId) { }
        public void Disconnect(Guid deviceId) { }
        public bool IsConnected(Guid deviceId) => ConnectedDeviceIds.Contains(deviceId);
        public LaserDeviceCapabilities GetCapabilities(Guid deviceId) => LaserDeviceCapabilities.None;
        public void ConnectAllAndReadWaveSettings() { }
        public LaserWaveSettings? GetCachedWaveSettings(Guid deviceId) => null;
        public LaserWaveSettings ReadWaveSettings(Guid deviceId) => ThrowCommunicationRead<LaserWaveSettings>();
        public LaserWaveSettings ReadWaveNumberSettings(Guid deviceId, int number) => ThrowCommunicationRead<LaserWaveSettings>();
        public LaserWaveSettings WriteWaveSettings(Guid deviceId, LaserWaveSettings settings) => throw new NotSupportedException();
        public LaserWaveSettings SwitchWave(Guid deviceId, int waveNumber) => throw new NotSupportedException();
        public HwdLaserWaveSettings? GetCachedHwdWaveSettings(Guid deviceId) => null;
        public HwdLaserWaveSettings ReadHwdWaveSettings(Guid deviceId) => ThrowCommunicationRead<HwdLaserWaveSettings>();
        public HwdLaserWaveSettings ReadHwdWaveNumberSettings(Guid deviceId, int waveNumber) => ThrowCommunicationRead<HwdLaserWaveSettings>();
        public HwdLaserWaveSettings WriteHwdWaveSettings(Guid deviceId, HwdLaserWaveSettings settings) => throw new NotSupportedException();
        public HwdLaserWaveSettings SwitchHwdWave(Guid deviceId, int waveNumber) => throw new NotSupportedException();
        public HwdLaserSystemSettings? GetCachedHwdSystemSettings(Guid deviceId) => null;
        public HwdLaserSystemSettings ReadHwdSystemSettings(Guid deviceId) => ThrowCommunicationRead<HwdLaserSystemSettings>();
        public HwdLaserSystemSettings WriteHwdSystemSettings(Guid deviceId, HwdLaserSystemSettings settings) => throw new NotSupportedException();
        public LaserSystemSettings? GetCachedSystemSettings(Guid deviceId) => null;
        public LaserSystemSettings ReadSystemSettings(Guid deviceId) => ThrowCommunicationRead<LaserSystemSettings>();
        public LaserSystemSettings WriteSystemSettings(Guid deviceId, LaserSystemSettings settings) => throw new NotSupportedException();
        public LaserRealtimeStatus? GetCachedRealtimeStatus(Guid deviceId) => null;

        public LaserDisplaySnapshot? GetCachedDisplaySnapshot(Guid deviceId)
        {
            SnapshotRequests.Add(deviceId);
            return Snapshots.GetValueOrDefault(deviceId);
        }

        public LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId) => ThrowCommunicationRead<LaserRealtimeStatus>();
        public LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId) => ThrowCommunicationRead<LaserRealtimeStatus>();
        public void SetContinuousOutput(Guid deviceId, bool enabled) { }
        public void PointWeld(Guid deviceId) { }
        public void SetLaserLock(Guid deviceId, bool locked) { }
        public void SetRedLight(Guid deviceId, bool enabled) { }
        public void ClearPointCount(Guid deviceId) { }
        public void ClearError(Guid deviceId) { }

        private T ThrowCommunicationRead<T>()
        {
            CommunicationReadCount++;
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingTemperaturePlcService : IPlcService
    {
        public Dictionary<PlcAddress, bool> BoolValues { get; } = [];

        public Dictionary<PlcAddress, float> FloatValues { get; } = [];

        public List<(PlcAddress Address, int Value)> IntWrites { get; } = [];

        public List<(PlcAddress Address, float Value)> FloatWrites { get; } = [];

        public int ConnectCount { get; private set; }

        public string IpAddress { get; set; } = "192.168.0.10";

        public bool IsConnected { get; private set; } = true;

        public void Connect()
        {
            ConnectCount++;
            IsConnected = true;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public bool ReadBool(PlcAddress address)
        {
            return BoolValues.GetValueOrDefault(address);
        }

        public int ReadInt(PlcAddress address)
        {
            return 0;
        }

        public float ReadFloat(PlcAddress address)
        {
            return FloatValues.GetValueOrDefault(address);
        }

        public void WriteBool(PlcAddress address, bool value)
        {
            BoolValues[address] = value;
        }

        public void WriteInt(PlcAddress address, int value)
        {
            IntWrites.Add((address, value));
        }

        public void WriteDInt(PlcAddress address, int value)
        {
        }

        public void WriteFloat(PlcAddress address, float value)
        {
            FloatWrites.Add((address, value));
            FloatValues[address] = value;
        }
    }

    private sealed class RecordingRecipeConfigStore : IRecipeConfigStore
    {
        public RecipeSettings LoadedSettings { get; set; } = new();

        public RecipeSettings Load() => LoadedSettings;

        public void Save(RecipeSettings settings)
        {
        }
    }
}
