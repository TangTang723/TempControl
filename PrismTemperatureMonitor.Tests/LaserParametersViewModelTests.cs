using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;
using PrismTemperatureMonitor.ViewModels;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;

namespace PrismTemperatureMonitor.Tests;

public sealed class LaserParametersViewModelTests
{
    [Fact]
    public void AddDeviceCommand_AddsDeviceAndPersistsConfiguration()
    {
        var service = new RecordingLaserDeviceService();
        var viewModel = new LaserParametersViewModel(service)
        {
            NewDeviceName = "激光器A",
            NewPortName = "COM3"
        };

        viewModel.AddDeviceCommand.Execute();

        var device = Assert.Single(viewModel.Devices);
        Assert.Equal("激光器A", device.Name);
        Assert.Equal("COM3", device.PortName);
        Assert.Single(service.SavedDevices);
        Assert.Equal(device.Id, viewModel.SelectedDevice?.Id);
    }

    [Fact]
    public void HwdDeviceAddress_IsAlwaysDefaultAndPowerScaleVisibilityFollowsModel()
    {
        var service = new RecordingLaserDeviceService();
        var viewModel = new LaserParametersViewModel(service)
        {
            NewDeviceModel = LaserDeviceModel.HWD,
            NewHwdPowerScale = HwdPowerScale.PointOneW
        };

        Assert.True(viewModel.IsNewDeviceHwd);

        viewModel.AddDeviceCommand.Execute();

        var saved = Assert.Single(service.SavedDevices);
        Assert.Equal(0x01, saved.DeviceAddress);
        Assert.Equal(HwdPowerScale.PointOneW, saved.HwdPowerScale);

        viewModel.NewDeviceModel = LaserDeviceModel.HWQ;

        Assert.False(viewModel.IsNewDeviceHwd);
    }

    [Fact]
    public void NewViewModel_UsesApplicationInitializedDeviceCaches()
    {
        var service = new RecordingLaserDeviceService();
        var first = service.AddSeedDevice("激光器A", "COM1");
        var second = service.AddSeedDevice("激光器B", "COM2");
        service.ReadResults[first.Id] = CreateSettings(1, 11);
        service.ReadResults[second.Id] = CreateSettings(2, 22);
        service.SystemReadResults[first.Id] = CreateSystemSettings(1, true);
        service.SystemReadResults[second.Id] = CreateSystemSettings(0, false);

        InitializeDevices(service);
        var viewModel = new LaserParametersViewModel(service);

        Assert.Equal([first.Id, second.Id], service.AutoConnectedDevices);
        Assert.True(viewModel.Devices.All(device => device.IsConnected));
        Assert.Equal(1, viewModel.CurrentWaveSettings.WaveNumber);
        Assert.Equal(11, viewModel.CurrentWaveSettings.OutputFrequency);
        Assert.Equal(1, viewModel.CurrentSystemSettings.LaserTriggerModeIndex);
        Assert.Equal("电平出光", viewModel.DisplayTriggerMode);
    }

    [Fact]
    public void SelectingDifferentDevice_ShowsCachedSettingsWithoutReadingAgain()
    {
        var service = new RecordingLaserDeviceService();
        var first = service.AddSeedDevice("激光器A", "COM1");
        var second = service.AddSeedDevice("激光器B", "COM2");
        service.CachedSettings[first.Id] = CreateSettings(1, 11);
        service.CachedSettings[second.Id] = CreateSettings(2, 22);
        service.ReadResults[first.Id] = CreateSettings(1, 11);
        service.ReadResults[second.Id] = CreateSettings(2, 22);
        service.SystemCachedSettings[first.Id] = CreateSystemSettings(0, false);
        service.SystemCachedSettings[second.Id] = CreateSystemSettings(1, true);
        service.SystemReadResults[first.Id] = CreateSystemSettings(0, false);
        service.SystemReadResults[second.Id] = CreateSystemSettings(1, true);
        var viewModel = new LaserParametersViewModel(service);
        service.ReadDevices.Clear();
        service.SystemReadDevices.Clear();

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.Id == second.Id);

        Assert.Equal(2, viewModel.CurrentWaveSettings.WaveNumber);
        Assert.Equal(22, viewModel.CurrentWaveSettings.OutputFrequency);
        Assert.Equal(1, viewModel.CurrentSystemSettings.LaserTriggerModeIndex);
        Assert.True(viewModel.CurrentSystemSettings.RedLightExternalEnabled);
        Assert.Empty(service.ReadDevices);
        Assert.Empty(service.SystemReadDevices);
    }

    [Fact]
    public void SelectingDevice_ShowsCachedRealtimeStatus()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM1");
        service.RealtimeCachedStatuses[device.Id] = new LaserRealtimeStatus
        {
            LaserPower = 180,
            OutputPointCount = 25,
            MachineAlarmByte = 0x01,
            MachineLightByte = 0x20,
            AnalogInput = 37
        };

        var viewModel = new LaserParametersViewModel(service);

        Assert.Equal("180", viewModel.DisplayLaserPower);
        Assert.Equal("25", viewModel.DisplayOutputPointCount);
        Assert.Equal("3.70", viewModel.DisplayAnalogVoltage);
        Assert.Equal("出光中", viewModel.DisplayOutputStatus);
        Assert.Equal("正常", viewModel.DisplayAlarmStatus);
    }

    [Fact]
    public void RealtimeRefresh_UsesCachedStatusWithoutCommunication()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM1");
        var viewModel = new LaserParametersViewModel(service);
        service.Connect(device.Id);
        service.RealtimeCachedStatuses[device.Id] = new LaserRealtimeStatus { LaserPower = 120 };

        InvokeCachedRealtimeRefresh(viewModel);

        Assert.Empty(service.RealtimeReadDevices);
        Assert.Empty(service.HwdFastRealtimeReadDevices);
        Assert.Equal("120", viewModel.DisplayLaserPower);
    }

    [Fact]
    public void SelectingDevice_LoadsDeviceIntoSharedEditor()
    {
        var service = new RecordingLaserDeviceService();
        service.AddSeedDevice("激光器A", "COM1");
        var second = service.AddSeedDevice("激光器B", "COM8");
        var viewModel = new LaserParametersViewModel(service);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.Id == second.Id);

        Assert.Equal("激光器B", viewModel.NewDeviceName);
        Assert.Equal("COM8", viewModel.NewPortName);
        Assert.Equal(115200, viewModel.NewBaudRate);
    }

    [Fact]
    public void SaveSelectedDeviceCommand_UsesSharedEditorValues()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM1");
        var viewModel = new LaserParametersViewModel(service)
        {
            NewDeviceName = "激光器A-改",
            NewPortName = "COM9",
            NewBaudRate = 57600
        };

        viewModel.SaveSelectedDeviceCommand.Execute();

        var saved = Assert.Single(service.SavedDevices);
        Assert.Equal(device.Id, saved.Id);
        Assert.Equal("激光器A-改", saved.Name);
        Assert.Equal("COM9", saved.PortName);
        Assert.Equal(57600, saved.BaudRate);
    }

    [Fact]
    public void SaveWaveCommand_WritesSelectedWaveSettings()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM3");
        service.WriteResult = CreateSettings(3, 66);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        viewModel.CurrentWaveSettings.WaveNumber = 3;
        viewModel.CurrentWaveSettings.OutputFrequency = 66;

        viewModel.SaveWaveCommand.Execute();

        Assert.Equal(device.Id, service.WrittenDeviceId);
        Assert.Equal(3, service.WrittenSettings!.WaveNumber);
    }

    [Fact]
    public void SwitchWaveCommand_SwitchesSelectedDeviceWaveAndRefreshesSettings()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM3");
        service.SwitchResult = CreateSettings(5, 77);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        viewModel.CurrentWaveSettings.WaveNumber = 5;

        viewModel.SwitchWaveCommand.Execute();

        Assert.Equal(device.Id, service.SwitchedDeviceId);
        Assert.Equal(5, service.SwitchedWaveNumber);
        Assert.Equal(77, viewModel.CurrentWaveSettings.OutputFrequency);
    }

    [Fact]
    public void ChangingWaveNumber_ReadsSelectedWaveSettingsAndRefreshesWaveChart()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM3");
        service.ReadWaveNumberResults[(device.Id, 2)] = CreateSettings(2, 88);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        var series = Assert.IsType<LineSeries<ObservablePoint>>(viewModel.Series[0]);
        var points = Assert.IsAssignableFrom<ObservableCollection<ObservablePoint>>(series.Values);
        var refreshCount = 0;
        var segmentSourceRefreshCount = 0;
        points.CollectionChanged += (_, _) => refreshCount++;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LaserParametersViewModel.CurrentWaveSegments))
            {
                segmentSourceRefreshCount++;
            }
        };

        viewModel.CurrentWaveSettings.WaveNumber = 2;

        Assert.Contains((device.Id, 2), service.ReadWaveNumberRequests);
        Assert.Null(service.SwitchedDeviceId);
        Assert.Equal(88, viewModel.CurrentWaveSettings.OutputFrequency);
        Assert.Same(viewModel.CurrentWaveSettings.Segments, viewModel.CurrentWaveSegments);
        Assert.Equal(88, points[0].Y);
        Assert.True(refreshCount > 0);
        Assert.True(segmentSourceRefreshCount > 0);
    }

    [Fact]
    public void ChangingWaveMode_ResetsWaveSettingsWithoutDeviceCommunication()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("婵€鍏夊櫒A", "COM3");
        var initialSettings = CreateSettings(3, 88);
        initialSettings.WaveModeIndex = 0;
        service.CachedSettings[device.Id] = initialSettings.Clone();
        service.ReadResults[device.Id] = initialSettings.Clone();
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        service.ReadDevices.Clear();
        service.ReadWaveNumberRequests.Clear();

        viewModel.CurrentWaveSettings.WaveModeIndex = 1;

        Assert.Empty(service.ReadDevices);
        Assert.Empty(service.ReadWaveNumberRequests);
        Assert.Null(service.SwitchedDeviceId);
        Assert.Equal(3, viewModel.CurrentWaveSettings.WaveNumber);
        Assert.Equal(1, viewModel.CurrentWaveSettings.WaveModeIndex);
        Assert.Equal(0, viewModel.CurrentWaveSettings.OutputFrequency);
        Assert.Equal(0, viewModel.CurrentWaveSettings.MaximumPeakPower);
        Assert.Equal(0, viewModel.CurrentWaveSettings.MaximumAveragePower);
        Assert.Equal(0, viewModel.CurrentWaveSettings.PresetLaserEnergy);
        Assert.All(viewModel.CurrentWaveSettings.Segments, segment =>
        {
            Assert.Equal(0, segment.Time);
            Assert.Equal(0, segment.Power);
        });
    }

    [Fact]
    public void WaveModeLabels_ChangeBetweenQcwAndCw()
    {
        var service = new RecordingLaserDeviceService();
        service.AddSeedDevice("婵€鍏夊櫒A", "COM3");
        var viewModel = new LaserParametersViewModel(service);

        viewModel.CurrentWaveSettings.WaveModeIndex = 0;

        Assert.Equal("出光频率", viewModel.WaveFrequencyLabel);
        Assert.Equal("预设激光能量", viewModel.PresetEnergyLabel);
        Assert.Equal("最高出光频率", viewModel.MaxOutputFrequencyLabel);
        Assert.Equal("能量报警上下限", viewModel.AlarmRangeLabel);
        Assert.Equal("时间(0.01ms)", viewModel.WaveSegmentTimeHeader);

        viewModel.CurrentWaveSettings.WaveModeIndex = 1;

        Assert.Equal("调制频率", viewModel.WaveFrequencyLabel);
        Assert.Equal("预设激光功率", viewModel.PresetEnergyLabel);
        Assert.Equal("占空比", viewModel.MaxOutputFrequencyLabel);
        Assert.Equal("功率上下限", viewModel.AlarmRangeLabel);
        Assert.Equal("时间(0.1ms)", viewModel.WaveSegmentTimeHeader);
    }

    [Fact]
    public void OutputCommands_CallSelectedDeviceOperations()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM3");
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;

        viewModel.ToggleContinuousOutputCommand.Execute();
        viewModel.PointWeldCommand.Execute();
        viewModel.ToggleLaserLockCommand.Execute();
        viewModel.ToggleRedLightCommand.Execute();
        viewModel.ClearPointCountCommand.Execute();
        viewModel.ClearErrorCommand.Execute();

        Assert.Equal(device.Id, service.ContinuousOutputDeviceId);
        Assert.Equal(device.Id, service.PointWeldDeviceId);
        Assert.Equal(device.Id, service.LaserLockDeviceId);
        Assert.Equal(device.Id, service.RedLightDeviceId);
        Assert.Equal(device.Id, service.ClearPointCountDeviceId);
        Assert.Equal(device.Id, service.ClearErrorDeviceId);
    }

    [Fact]
    public void SaveSystemSettingsCommand_WritesSelectedDeviceSettings()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("激光器A", "COM3");
        service.SystemWriteResult = CreateSystemSettings(1, true);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        viewModel.CurrentSystemSettings.LaserTriggerModeIndex = 1;
        viewModel.CurrentSystemSettings.RedLightExternalEnabled = true;
        viewModel.CurrentSystemSettings.AnalogModulationEnabled = true;

        viewModel.SaveSystemSettingsCommand.Execute();

        Assert.Equal(device.Id, service.SystemWrittenDeviceId);
        Assert.Equal(1, service.SystemWrittenSettings!.LaserTriggerModeIndex);
        Assert.True(service.SystemWrittenSettings.RedLightExternalEnabled);
        Assert.True(service.SystemWrittenSettings.AnalogModulationEnabled);
        Assert.Equal("电平出光", viewModel.DisplayTriggerMode);
    }

    [Fact]
    public void SelectingHwdDevice_ShowsHwdWaveSettingsAndCapabilities()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        service.HwdCachedSettings[device.Id] = CreateHwdSettings(2, 80);
        service.HwdReadResults[device.Id] = CreateHwdSettings(2, 80);

        var viewModel = new LaserParametersViewModel(service);

        Assert.True(viewModel.IsHwdSelected);
        Assert.False(viewModel.IsHwqSelected);
        Assert.Equal(2, viewModel.CurrentHwdWaveSettings.WaveNumber);
        Assert.Equal(80, viewModel.CurrentHwdWaveSettings.MaximumPower);
    }

    [Fact]
    public void ChangingHwdWaveNumber_ReadsOnlyRequestedHwdWave()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        service.HwdReadResults[device.Id] = CreateHwdSettings(1, 50);
        service.HwdReadWaveNumberResults[(device.Id, 6)] = CreateHwdSettings(6, 120);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        service.HwdReadWaveNumberRequests.Clear();

        viewModel.CurrentHwdWaveSettings.WaveNumber = 6;

        Assert.Contains((device.Id, 6), service.HwdReadWaveNumberRequests);
        Assert.Null(service.HwdSwitchedDeviceId);
        Assert.Equal(120, viewModel.CurrentHwdWaveSettings.MaximumPower);
    }

    [Fact]
    public void HwdParameterDisplay_UsesLastReadWaveSnapshot()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        var readSettings = CreateHwdSettings(1, 120);
        readSettings.AveragePower = 45.5;
        service.HwdReadResults[device.Id] = readSettings;
        InitializeDevices(service);
        var viewModel = new LaserParametersViewModel(service);

        viewModel.CurrentHwdWaveSettings.MaximumPower = 250;
        viewModel.CurrentHwdWaveSettings.AveragePower = 80;

        Assert.Equal("120", viewModel.DisplayLaserPower);
        Assert.Equal("45.5", viewModel.DisplayAveragePower);
    }

    [Fact]
    public void HwdRealtimeRefresh_UsesSharedCachedStatus()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        service.RealtimeCachedStatuses[device.Id] = new LaserRealtimeStatus
        {
            LaserPower = 300,
            OutputPointCount = 123456,
            MachineLightByte = 0x20,
            MachineAlarmByte = 0x00
        };
        var viewModel = new LaserParametersViewModel(service);
        service.Connect(device.Id);

        InvokeCachedRealtimeRefresh(viewModel);

        Assert.Equal(300, viewModel.CurrentRealtimeStatus.LaserPower);
        Assert.Equal(123456, viewModel.CurrentRealtimeStatus.OutputPointCount);
        Assert.True(viewModel.CurrentRealtimeStatus.IsLaserOutputActive);
        Assert.True(viewModel.CurrentRealtimeStatus.IsAlarmActive);
        Assert.Empty(service.HwdFastRealtimeReadDevices);
        Assert.Empty(service.RealtimeReadDevices);
    }

    [Fact]
    public void ChangingHwdEmitMode_ResetsEditingDataWithoutDeviceCommunication()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        var initial = CreateHwdSettings(3, 120);
        initial.CurrentOutputWaveNumber = 2;
        initial.PulseInterval = 12.3;
        initial.ModulationPeriod = 30;
        initial.OutputRatio = 75;
        initial.Segments[0].Time = 2;
        initial.Segments[0].Power = 80;
        service.HwdReadResults[device.Id] = initial;
        InitializeDevices(service);
        var viewModel = new LaserParametersViewModel(service);
        service.HwdReadWaveNumberRequests.Clear();

        viewModel.CurrentHwdWaveSettings.EmitMode = HwdEmitMode.FCW;

        Assert.Empty(service.HwdReadWaveNumberRequests);
        Assert.Equal(3, viewModel.CurrentHwdWaveSettings.WaveNumber);
        Assert.Equal(2, viewModel.CurrentHwdWaveSettings.CurrentOutputWaveNumber);
        Assert.Equal(HwdEmitMode.FCW, viewModel.CurrentHwdWaveSettings.EmitMode);
        Assert.Equal(HwdTriggerMode.电平出光, viewModel.CurrentHwdWaveSettings.TriggerMode);
        Assert.Equal(0, viewModel.CurrentHwdWaveSettings.MaximumPower);
        Assert.Equal(0, viewModel.CurrentHwdWaveSettings.PulseInterval);
        Assert.Equal(0, viewModel.CurrentHwdWaveSettings.ModulationPeriod);
        Assert.Equal(0, viewModel.CurrentHwdWaveSettings.OutputRatio);
        Assert.All(viewModel.CurrentHwdWaveSettings.Segments, segment =>
        {
            Assert.Equal(0, segment.Time);
            Assert.Equal(0, segment.Power);
        });
    }

    [Fact]
    public void HwdSaveAndSwitchCommands_UseHwdOperations()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        service.HwdReadResults[device.Id] = CreateHwdSettings(1, 50);
        service.HwdWriteResult = CreateHwdSettings(3, 90);
        service.HwdSwitchResult = CreateHwdSettings(4, 100);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;

        viewModel.CurrentHwdWaveSettings.WaveNumber = 3;
        viewModel.SaveWaveCommand.Execute();
        viewModel.CurrentHwdWaveSettings.WaveNumber = 4;
        viewModel.SwitchWaveCommand.Execute();

        Assert.Equal(device.Id, service.HwdWrittenDeviceId);
        Assert.Equal(3, service.HwdWrittenSettings?.WaveNumber);
        Assert.Equal(device.Id, service.HwdSwitchedDeviceId);
        Assert.Equal(4, service.HwdSwitchedWaveNumber);
    }

    [Fact]
    public void SelectingHwdDevice_LoadsHwdSystemSettings()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        service.HwdSystemReadResults[device.Id] = new HwdLaserSystemSettings
        {
            WaveExternalEnabled = true,
            LaserTriggerExternalEnabled = true
        };

        InitializeDevices(service);
        var viewModel = new LaserParametersViewModel(service);

        Assert.True(viewModel.CurrentHwdSystemSettings.WaveExternalEnabled);
        Assert.True(viewModel.CurrentHwdSystemSettings.LaserTriggerExternalEnabled);
        Assert.Equal(HwdTriggerMode.脉冲出光, viewModel.CurrentHwdWaveSettings.TriggerMode);
    }

    [Fact]
    public void SaveSystemSettingsCommand_WritesHwdSystemSettings()
    {
        var service = new RecordingLaserDeviceService();
        var device = service.AddSeedDevice("HWD", "COM8", LaserDeviceModel.HWD);
        var viewModel = new LaserParametersViewModel(service);
        viewModel.SelectedDevice!.IsConnected = true;
        viewModel.CurrentHwdSystemSettings.WaveExternalEnabled = true;
        viewModel.CurrentHwdSystemSettings.LaserTriggerExternalEnabled = true;

        viewModel.SaveSystemSettingsCommand.Execute();

        Assert.Equal(device.Id, service.HwdSystemWrittenDeviceId);
        Assert.True(service.HwdSystemWrittenSettings?.WaveExternalEnabled);
        Assert.True(service.HwdSystemWrittenSettings?.LaserTriggerExternalEnabled);
    }

    private static LaserWaveSettings CreateSettings(int waveNumber, short outputFrequency)
    {
        var settings = new LaserWaveSettings
        {
            WaveNumber = waveNumber,
            WaveModeIndex = 1,
            OutputFrequency = outputFrequency,
            MaximumPeakPower = 500,
            MaximumAveragePower = 80,
            AverageFrequency = 20,
            EnergyAlarmUpper = 100,
            EnergyAlarmLower = 5,
            PresetLaserEnergy = 30,
            MaxOutputFrequency = 200
        };
        settings.Segments[0].Time = waveNumber;
        settings.Segments[0].Power = outputFrequency;
        return settings;
    }

    private static void InitializeDevices(ILaserDeviceService service)
    {
        new LaserDeviceStartupInitializer(service, new NoopLaserRealtimePollingService()).Initialize();
    }

    private sealed class NoopLaserRealtimePollingService : ILaserRealtimePollingService
    {
        public void Start()
        {
        }
    }

    private static LaserSystemSettings CreateSystemSettings(int triggerModeIndex, bool redLightExternalEnabled)
    {
        return new LaserSystemSettings
        {
            LaserTriggerModeIndex = triggerModeIndex,
            RedLightExternalEnabled = redLightExternalEnabled,
            WaveExternalEnabled = true,
            LaserTriggerExternalEnabled = true,
            AnalogModulationEnabled = true,
            DigitalModulationEnabled = false
        };
    }

    private static HwdLaserWaveSettings CreateHwdSettings(int waveNumber, double maximumPower)
    {
        var settings = new HwdLaserWaveSettings
        {
            CurrentOutputWaveNumber = 1,
            WaveNumber = waveNumber,
            EmitMode = HwdEmitMode.SAW,
            TriggerMode = HwdTriggerMode.脉冲出光,
            MaximumPower = maximumPower
        };
        settings.Segments[0].Power = maximumPower;
        return settings;
    }

    private static void InvokeCachedRealtimeRefresh(LaserParametersViewModel viewModel)
    {
        var method = typeof(LaserParametersViewModel).GetMethod(
            "RefreshSelectedDeviceCachedRealtimeStatus",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(viewModel, []);
    }

    private sealed class RecordingLaserDeviceService : ILaserDeviceService
    {
        private readonly List<LaserDeviceConfig> _devices = [];

        public HashSet<Guid> ConnectedDevices { get; } = [];

        public Dictionary<Guid, LaserWaveSettings> ReadResults { get; } = [];

        public Dictionary<(Guid DeviceId, int WaveNumber), LaserWaveSettings> ReadWaveNumberResults { get; } = [];

        public Dictionary<Guid, LaserWaveSettings> CachedSettings { get; } = [];

        public Dictionary<Guid, LaserSystemSettings> SystemReadResults { get; } = [];

        public Dictionary<Guid, LaserSystemSettings> SystemCachedSettings { get; } = [];

        public Dictionary<Guid, LaserRealtimeStatus> RealtimeReadResults { get; } = [];

        public Dictionary<Guid, LaserRealtimeStatus> RealtimeCachedStatuses { get; } = [];

        public Dictionary<Guid, LaserRealtimeStatus> HwdFastRealtimeReadResults { get; } = [];

        public Dictionary<Guid, HwdLaserWaveSettings> HwdReadResults { get; } = [];

        public Dictionary<Guid, HwdLaserWaveSettings> HwdCachedSettings { get; } = [];

        public Dictionary<(Guid DeviceId, int WaveNumber), HwdLaserWaveSettings> HwdReadWaveNumberResults { get; } = [];

        public List<(Guid DeviceId, int WaveNumber)> HwdReadWaveNumberRequests { get; } = [];

        public Dictionary<Guid, HwdLaserSystemSettings> HwdSystemReadResults { get; } = [];

        public Dictionary<Guid, HwdLaserSystemSettings> HwdSystemCachedSettings { get; } = [];

        public List<Guid> ReadDevices { get; } = [];

        public List<(Guid DeviceId, int WaveNumber)> ReadWaveNumberRequests { get; } = [];

        public List<Guid> SystemReadDevices { get; } = [];

        public List<Guid> RealtimeReadDevices { get; } = [];

        public List<Guid> HwdFastRealtimeReadDevices { get; } = [];

        public List<LaserDeviceConfig> SavedDevices { get; private set; } = [];

        public Guid? WrittenDeviceId { get; private set; }

        public LaserWaveSettings? WrittenSettings { get; private set; }

        public LaserWaveSettings? WriteResult { get; set; }

        public LaserWaveSettings? SwitchResult { get; set; }

        public List<Guid> AutoConnectedDevices { get; } = [];

        public Guid? SwitchedDeviceId { get; private set; }

        public int? SwitchedWaveNumber { get; private set; }

        public Guid? ContinuousOutputDeviceId { get; private set; }

        public Guid? PointWeldDeviceId { get; private set; }

        public Guid? LaserLockDeviceId { get; private set; }

        public Guid? RedLightDeviceId { get; private set; }

        public Guid? ClearPointCountDeviceId { get; private set; }

        public Guid? ClearErrorDeviceId { get; private set; }

        public Guid? SystemWrittenDeviceId { get; private set; }

        public LaserSystemSettings? SystemWrittenSettings { get; private set; }

        public LaserSystemSettings? SystemWriteResult { get; set; }

        public Guid? HwdWrittenDeviceId { get; private set; }

        public HwdLaserWaveSettings? HwdWrittenSettings { get; private set; }

        public HwdLaserWaveSettings? HwdWriteResult { get; set; }

        public Guid? HwdSwitchedDeviceId { get; private set; }

        public int? HwdSwitchedWaveNumber { get; private set; }

        public HwdLaserWaveSettings? HwdSwitchResult { get; set; }

        public Guid? HwdSystemWrittenDeviceId { get; private set; }

        public HwdLaserSystemSettings? HwdSystemWrittenSettings { get; private set; }

        public IReadOnlyList<LaserDeviceConfig> GetDevices()
        {
            return _devices.Select(device => device.Clone()).ToArray();
        }

        public void SaveDevices(IEnumerable<LaserDeviceConfig> devices)
        {
            SavedDevices = devices.Select(device => device.Clone()).ToList();
            _devices.Clear();
            _devices.AddRange(SavedDevices.Select(device => device.Clone()));
        }

        public void AddOrUpdateDevice(LaserDeviceConfig device)
        {
            _devices.RemoveAll(existing => existing.Id == device.Id);
            _devices.Add(device.Clone());
        }

        public void RemoveDevice(Guid deviceId)
        {
            _devices.RemoveAll(device => device.Id == deviceId);
            ConnectedDevices.Remove(deviceId);
        }

        public void Connect(Guid deviceId)
        {
            ConnectedDevices.Add(deviceId);
        }

        public void Disconnect(Guid deviceId)
        {
            ConnectedDevices.Remove(deviceId);
        }

        public bool IsConnected(Guid deviceId)
        {
            return ConnectedDevices.Contains(deviceId);
        }

        public LaserDeviceCapabilities GetCapabilities(Guid deviceId)
        {
            return _devices.Single(device => device.Id == deviceId).Model == LaserDeviceModel.HWD
                ? LaserDeviceCapabilities.CommonControl
                    | LaserDeviceCapabilities.RealtimeStatus
                    | LaserDeviceCapabilities.HwdWaveSettings
                    | LaserDeviceCapabilities.HwdSystemSettings
                : LaserDeviceCapabilities.CommonControl
                    | LaserDeviceCapabilities.RealtimeStatus
                    | LaserDeviceCapabilities.HwqWaveSettings
                    | LaserDeviceCapabilities.HwqSystemSettings;
        }

        public void ConnectAllAndReadWaveSettings()
        {
            foreach (var device in _devices)
            {
                Connect(device.Id);
                if (device.Model == LaserDeviceModel.HWD)
                {
                    ReadHwdWaveSettings(device.Id);
                    ReadHwdSystemSettings(device.Id);
                }
                else
                {
                    ReadWaveSettings(device.Id);
                    ReadSystemSettings(device.Id);
                }
                AutoConnectedDevices.Add(device.Id);
            }
        }

        public LaserWaveSettings? GetCachedWaveSettings(Guid deviceId)
        {
            return CachedSettings.GetValueOrDefault(deviceId)?.Clone();
        }

        public LaserWaveSettings ReadWaveSettings(Guid deviceId)
        {
            ReadDevices.Add(deviceId);
            var settings = ReadResults.GetValueOrDefault(deviceId, CreateSettings(1, 10)).Clone();
            CachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public LaserWaveSettings ReadWaveNumberSettings(Guid deviceId, int number)
        {
            ReadWaveNumberRequests.Add((deviceId, number));
            var settings = ReadWaveNumberResults.GetValueOrDefault((deviceId, number), CreateSettings(number, 10)).Clone();
            CachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public LaserWaveSettings WriteWaveSettings(Guid deviceId, LaserWaveSettings settings)
        {
            WrittenDeviceId = deviceId;
            WrittenSettings = settings.Clone();
            var result = WriteResult?.Clone() ?? settings.Clone();
            CachedSettings[deviceId] = result.Clone();
            return result;
        }

        public LaserWaveSettings SwitchWave(Guid deviceId, int waveNumber)
        {
            SwitchedDeviceId = deviceId;
            SwitchedWaveNumber = waveNumber;
            var result = SwitchResult?.Clone() ?? CreateSettings(waveNumber, 10);
            CachedSettings[deviceId] = result.Clone();
            return result;
        }

        public HwdLaserWaveSettings? GetCachedHwdWaveSettings(Guid deviceId)
        {
            return HwdCachedSettings.GetValueOrDefault(deviceId)?.Clone();
        }

        public HwdLaserWaveSettings ReadHwdWaveSettings(Guid deviceId)
        {
            var settings = HwdReadResults.GetValueOrDefault(deviceId, CreateHwdSettings(1, 10)).Clone();
            HwdCachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public HwdLaserWaveSettings ReadHwdWaveNumberSettings(Guid deviceId, int waveNumber)
        {
            HwdReadWaveNumberRequests.Add((deviceId, waveNumber));
            var settings = HwdReadWaveNumberResults
                .GetValueOrDefault((deviceId, waveNumber), CreateHwdSettings(waveNumber, 10))
                .Clone();
            HwdCachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public HwdLaserWaveSettings WriteHwdWaveSettings(Guid deviceId, HwdLaserWaveSettings settings)
        {
            HwdWrittenDeviceId = deviceId;
            HwdWrittenSettings = settings.Clone();
            var result = HwdWriteResult?.Clone() ?? settings.Clone();
            HwdCachedSettings[deviceId] = result.Clone();
            return result;
        }

        public HwdLaserWaveSettings SwitchHwdWave(Guid deviceId, int waveNumber)
        {
            HwdSwitchedDeviceId = deviceId;
            HwdSwitchedWaveNumber = waveNumber;
            var result = HwdSwitchResult?.Clone() ?? CreateHwdSettings(waveNumber, 10);
            HwdCachedSettings[deviceId] = result.Clone();
            return result;
        }

        public HwdLaserSystemSettings? GetCachedHwdSystemSettings(Guid deviceId)
        {
            return HwdSystemCachedSettings.GetValueOrDefault(deviceId)?.Clone();
        }

        public HwdLaserSystemSettings ReadHwdSystemSettings(Guid deviceId)
        {
            var settings = HwdSystemReadResults
                .GetValueOrDefault(deviceId, new HwdLaserSystemSettings())
                .Clone();
            HwdSystemCachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public HwdLaserSystemSettings WriteHwdSystemSettings(
            Guid deviceId,
            HwdLaserSystemSettings settings)
        {
            HwdSystemWrittenDeviceId = deviceId;
            HwdSystemWrittenSettings = settings.Clone();
            HwdSystemCachedSettings[deviceId] = settings.Clone();
            return settings.Clone();
        }

        public LaserSystemSettings? GetCachedSystemSettings(Guid deviceId)
        {
            return SystemCachedSettings.GetValueOrDefault(deviceId)?.Clone();
        }

        public LaserSystemSettings ReadSystemSettings(Guid deviceId)
        {
            SystemReadDevices.Add(deviceId);
            var settings = SystemReadResults.GetValueOrDefault(deviceId, CreateSystemSettings(0, false)).Clone();
            SystemCachedSettings[deviceId] = settings.Clone();
            return settings;
        }

        public LaserSystemSettings WriteSystemSettings(Guid deviceId, LaserSystemSettings settings)
        {
            SystemWrittenDeviceId = deviceId;
            SystemWrittenSettings = settings.Clone();
            var result = SystemWriteResult?.Clone() ?? settings.Clone();
            SystemCachedSettings[deviceId] = result.Clone();
            return result;
        }

        public LaserRealtimeStatus? GetCachedRealtimeStatus(Guid deviceId)
        {
            return RealtimeCachedStatuses.GetValueOrDefault(deviceId)?.Clone();
        }

        public LaserDisplaySnapshot? GetCachedDisplaySnapshot(Guid deviceId)
        {
            return null;
        }

        public LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId)
        {
            RealtimeReadDevices.Add(deviceId);
            var status = RealtimeReadResults.GetValueOrDefault(deviceId, new LaserRealtimeStatus()).Clone();
            RealtimeCachedStatuses[deviceId] = status.Clone();
            return status;
        }

        public LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId)
        {
            HwdFastRealtimeReadDevices.Add(deviceId);
            var status = HwdFastRealtimeReadResults.GetValueOrDefault(deviceId, new LaserRealtimeStatus()).Clone();
            RealtimeCachedStatuses[deviceId] = status.Clone();
            return status;
        }

        public void SetContinuousOutput(Guid deviceId, bool enabled)
        {
            ContinuousOutputDeviceId = deviceId;
        }

        public void PointWeld(Guid deviceId)
        {
            PointWeldDeviceId = deviceId;
        }

        public void SetLaserLock(Guid deviceId, bool locked)
        {
            LaserLockDeviceId = deviceId;
        }

        public void SetRedLight(Guid deviceId, bool enabled)
        {
            RedLightDeviceId = deviceId;
        }

        public void ClearPointCount(Guid deviceId)
        {
            ClearPointCountDeviceId = deviceId;
        }

        public void ClearError(Guid deviceId)
        {
            ClearErrorDeviceId = deviceId;
        }

        public LaserDeviceConfig AddSeedDevice(
            string name,
            string portName,
            LaserDeviceModel model = LaserDeviceModel.HWQ)
        {
            var device = new LaserDeviceConfig
            {
                Id = Guid.NewGuid(),
                Name = name,
                PortName = portName,
                BaudRate = 115200,
                IsEnabled = true,
                Model = model
            };
            _devices.Add(device);
            return device.Clone();
        }
    }
}
