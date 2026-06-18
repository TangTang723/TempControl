using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Navigation.Regions;
using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;
using SkiaSharp;

namespace PrismTemperatureMonitor.ViewModels;

public sealed class LaserParametersViewModel : BindableBase, IDisposable, IDestructible, IRegionMemberLifetime
{
    private const int HwdFastPollingIntervalMilliseconds = 50;
    private static readonly Brush NormalStatusBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    private static readonly Brush WarningStatusBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));

    private readonly ILaserDeviceService _laserDeviceService;
    private readonly ObservableCollection<ObservablePoint> _wavePoints = [];
    private readonly DispatcherTimer _realtimeTimer;
    private readonly CancellationTokenSource _hwdFastPollingCancellation = new();
    private Thread? _hwdFastPollingThread;
    private LaserDeviceItemViewModel? _selectedDevice;
    private LaserWaveSettings _currentWaveSettings = new();
    private HwdLaserWaveSettings _currentHwdWaveSettings = new();
    private HwdLaserWaveSettings _hwdParameterSnapshot = new();
    private LaserSystemSettings _currentSystemSettings = new();
    private HwdLaserSystemSettings _currentHwdSystemSettings = new();
    private LaserRealtimeStatus _currentRealtimeStatus = new();
    private string _newDeviceName = "激光器";
    private string _newPortName = "COM1";
    private int _newBaudRate = 115200;
    private LaserDeviceModel _newDeviceModel = LaserDeviceModel.HWQ;
    private HwdPowerScale _newHwdPowerScale = HwdPowerScale.OneW;
    private string _operationMessage = "请选择或新增激光器。";
    private bool _isBusy;
    private bool _isContinuousOutputEnabled;
    private bool _isLaserLocked;
    private bool _isRedLightEnabled;
    private bool _isRefreshingRealtimeStatus;
    private bool _isApplyingWaveSettingsFromDevice;
    private bool _isApplyingHwdWaveSettingsFromDevice;
    private int _isDisposed;
    private DateTime _lastHwdRealtimeRefresh = DateTime.MinValue;

    public LaserParametersViewModel(ILaserDeviceService laserDeviceService)
    {
        _laserDeviceService = laserDeviceService;
        Devices = [];
        foreach (var device in _laserDeviceService.GetDevices())
        {
            Devices.Add(CreateDeviceItem(device));
        }

        WaveModes = ["QCW", "CW"];
        WaveNumbers = [1, 2, 3, 4, 5, 6, 7,8,9,10,11,12,13,14,15,16];
        DeviceModels = Enum.GetValues<LaserDeviceModel>();
        HwdPowerScales = Enum.GetValues<HwdPowerScale>();
        HwdEmitModes = Enum.GetValues<HwdEmitMode>();
        HwdTriggerModes = Enum.GetValues<HwdTriggerMode>();
        LaserTriggerModes = ["脉冲出光", "电平出光"];
        Series =
        [
            new LineSeries<ObservablePoint>
            {
                Name = "功率比",
                Values = _wavePoints,
                GeometrySize = 7,
                LineSmoothness = 0.55,
                Stroke = new SolidColorPaint(SKColor.Parse("#0EA5E9"), 3),
                Fill = null
            }
        ];
        XAxes =
        [
            new Axis
            {
                Name = "波段",
                MinLimit = 1,
                MaxLimit = 16,
                NamePaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
            }
        ];
        YAxes =
        [
            new Axis
            {
                Name = "功率比 / %",
                MinLimit = 0,
                MaxLimit = 100,
                NamePaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0"))
            }
        ];

        AddDeviceCommand = new DelegateCommand(AddDevice, () => !IsBusy)
            .ObservesProperty(() => IsBusy);
        RemoveSelectedDeviceCommand = new DelegateCommand(RemoveSelectedDevice, () => SelectedDevice is not null && !IsBusy)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        SaveSelectedDeviceCommand = new DelegateCommand(SaveSelectedDevice, () => SelectedDevice is not null && !IsBusy)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        SaveWaveCommand = new DelegateCommand(SaveWave, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        SwitchWaveCommand = new DelegateCommand(SwitchWave, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        ToggleContinuousOutputCommand = new DelegateCommand(ToggleContinuousOutput, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        PointWeldCommand = new DelegateCommand(PointWeld, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        ToggleLaserLockCommand = new DelegateCommand(ToggleLaserLock, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        ToggleRedLightCommand = new DelegateCommand(ToggleRedLight, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        ClearPointCountCommand = new DelegateCommand(ClearPointCount, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        ClearErrorCommand = new DelegateCommand(ClearError, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);
        SaveSystemSettingsCommand = new DelegateCommand(SaveSystemSettings, CanOperateSelectedDevice)
            .ObservesProperty(() => SelectedDevice)
            .ObservesProperty(() => IsBusy);

        _realtimeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _realtimeTimer.Tick += (_, _) => RefreshSelectedDeviceCachedRealtimeStatus();

        SelectedDevice = Devices.FirstOrDefault();
        SubscribeWaveSegmentChanges();
        UpdateWaveChart();
        _realtimeTimer.Start();
    }

    public ObservableCollection<LaserDeviceItemViewModel> Devices { get; }

    public IReadOnlyList<string> WaveModes { get; }

    public IReadOnlyList<int> WaveNumbers { get; }

    public IReadOnlyList<string> LaserTriggerModes { get; }

    public IReadOnlyList<LaserDeviceModel> DeviceModels { get; }

    public IReadOnlyList<HwdPowerScale> HwdPowerScales { get; }

    public IReadOnlyList<HwdEmitMode> HwdEmitModes { get; }

    public IReadOnlyList<HwdTriggerMode> HwdTriggerModes { get; }

    public ISeries[] Series { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public DelegateCommand AddDeviceCommand { get; }

    public DelegateCommand RemoveSelectedDeviceCommand { get; }

    public DelegateCommand SaveSelectedDeviceCommand { get; }

    public DelegateCommand SaveWaveCommand { get; }

    public DelegateCommand SwitchWaveCommand { get; }

    public DelegateCommand ToggleContinuousOutputCommand { get; }

    public DelegateCommand PointWeldCommand { get; }

    public DelegateCommand ToggleLaserLockCommand { get; }

    public DelegateCommand ToggleRedLightCommand { get; }

    public DelegateCommand ClearPointCountCommand { get; }

    public DelegateCommand ClearErrorCommand { get; }

    public DelegateCommand SaveSystemSettingsCommand { get; }

    public bool KeepAlive => false;

    public string ContinuousOutputText => IsContinuousOutputEnabled ? "连续出光(开)" : "连续出光(关)";

    public string LaserLockText => IsLaserLocked ? "锁光(开)" : "锁光(关)";

    public string RedLightText => IsRedLightEnabled ? "红光(开)" : "红光(关)";

    public string DisplayLaserPower => (IsHwdSelected
        ? HwdParameterSnapshot.MaximumPower
        : CurrentRealtimeStatus.LaserPower).ToString("0.##", CultureInfo.InvariantCulture);

    public string DisplayRealtimeLaserPower => CurrentRealtimeStatus.LaserPower.ToString(CultureInfo.InvariantCulture);

    public string DisplayWaveNumber => (IsHwdSelected
        ? HwdParameterSnapshot.WaveNumber
        : CurrentWaveSettings.WaveNumber).ToString(CultureInfo.InvariantCulture);

    public string DisplayWaveMode => IsHwdSelected
        ? HwdParameterSnapshot.EmitMode.ToString().ToUpperInvariant()
        : CurrentWaveSettings.WaveModeIndex >= 0 && CurrentWaveSettings.WaveModeIndex < WaveModes.Count
            ? WaveModes[CurrentWaveSettings.WaveModeIndex]
            : CurrentWaveSettings.WaveModeIndex.ToString(CultureInfo.InvariantCulture);

    public string DisplayTriggerMode => IsHwdSelected
        ? HwdParameterSnapshot.TriggerMode == HwdTriggerMode.电平出光 ? "电平触发" : "脉冲触发"
        : CurrentSystemSettings.LaserTriggerModeIndex >= 0 && CurrentSystemSettings.LaserTriggerModeIndex < LaserTriggerModes.Count
            ? LaserTriggerModes[CurrentSystemSettings.LaserTriggerModeIndex]
            : CurrentSystemSettings.LaserTriggerModeIndex.ToString(CultureInfo.InvariantCulture);

    public string DisplaySinglePointEnergy => (IsHwdSelected
        ? HwdParameterSnapshot.SinglePointEnergy
        : CurrentWaveSettings.PresetLaserEnergy).ToString("0.##", CultureInfo.InvariantCulture);

    public string DisplayOutputPointCount => CurrentRealtimeStatus.OutputPointCount.ToString(CultureInfo.InvariantCulture);

    public string DisplayAveragePower => (IsHwdSelected
        ? HwdParameterSnapshot.AveragePower
        : CurrentWaveSettings.MaximumAveragePower).ToString("0.##", CultureInfo.InvariantCulture);

    public string DisplayAnalogVoltage => CurrentRealtimeStatus.AnalogVoltage.ToString("0.00", CultureInfo.InvariantCulture);

    public string DisplayHwdPulseInterval => HwdParameterSnapshot.PulseInterval.ToString("0.###", CultureInfo.InvariantCulture);

    public string DisplayHwdModulationPeriod => HwdParameterSnapshot.ModulationPeriod.ToString("0.##", CultureInfo.InvariantCulture);

    public string DisplayHwdOutputRatio => HwdParameterSnapshot.OutputRatio.ToString("0.##", CultureInfo.InvariantCulture);

    public string DisplayHwdCurrentOutputWaveNumber => HwdParameterSnapshot.CurrentOutputWaveNumber.ToString(CultureInfo.InvariantCulture);

    public string WaveFrequencyLabel => IsCwWaveMode ? "调制频率" : "出光频率";

    public string PresetEnergyLabel => IsCwWaveMode ? "预设激光功率" : "预设激光能量";

    public string MaxOutputFrequencyLabel => IsCwWaveMode ? "占空比" : "最高出光频率";

    public string AlarmRangeLabel => IsCwWaveMode ? "功率上下限" : "能量报警上下限";

    public string WaveSegmentTimeHeader => IsCwWaveMode ? "时间(0.1ms)" : "时间(0.01ms)";

    public string WaveSegmentPowerHeader => "功率比 %";

    private bool IsCwWaveMode => CurrentWaveSettings.WaveModeIndex == 1;

    public bool IsHwqSelected => SelectedDevice?.Model != LaserDeviceModel.HWD;

    public bool IsHwdSelected => SelectedDevice?.Model == LaserDeviceModel.HWD;

    public bool CanEditHwdTrigger => CurrentHwdWaveSettings.EmitMode != HwdEmitMode.FCW;

    public bool IsHwdFcwMode => IsHwdSelected && CurrentHwdWaveSettings.EmitMode == HwdEmitMode.FCW;

    public bool IsHwdSegmentMode => IsHwdSelected && CurrentHwdWaveSettings.EmitMode != HwdEmitMode.FCW;

    public bool ShowWaveSegmentEditor => IsHwqSelected || IsHwdSegmentMode;

    public IEnumerable<LaserWaveSegment> CurrentWaveSegments => IsHwdSelected
        ? CurrentHwdWaveSettings.Segments
        : CurrentWaveSettings.Segments;

    public string DisplayOutputStatus => CurrentRealtimeStatus.IsLaserOutputActive || IsContinuousOutputEnabled ? "出光中" : "未出光";

    public string DisplayAlarmStatus => CurrentRealtimeStatus.IsAlarmActive ? "报警" : "正常";

    public Brush DisplayOutputStatusBrush => CurrentRealtimeStatus.IsLaserOutputActive || IsContinuousOutputEnabled
        ? WarningStatusBrush
        : NormalStatusBrush;

    public Brush DisplayAlarmStatusBrush => CurrentRealtimeStatus.IsAlarmActive ? WarningStatusBrush : NormalStatusBrush;

    public LaserDeviceItemViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                RaisePropertyChanged(nameof(IsHwqSelected));
                RaisePropertyChanged(nameof(IsHwdSelected));
                RaisePropertyChanged(nameof(IsHwdFcwMode));
                RaisePropertyChanged(nameof(IsHwdSegmentMode));
                RaisePropertyChanged(nameof(ShowWaveSegmentEditor));
                RaisePropertyChanged(nameof(CurrentWaveSegments));
                LoadSelectedDeviceToEditor();
                LoadSelectedDeviceCachedSettings();
                RaiseCommandStates();
            }
        }
    }

    public LaserWaveSettings CurrentWaveSettings
    {
        get => _currentWaveSettings;
        private set
        {
            var oldSettings = _currentWaveSettings;
            if (SetProperty(ref _currentWaveSettings, value))
            {
                oldSettings.PropertyChanged -= OnCurrentWaveSettingsChanged;
                CurrentWaveSettings.PropertyChanged -= OnCurrentWaveSettingsChanged;
                CurrentWaveSettings.PropertyChanged += OnCurrentWaveSettingsChanged;
                SubscribeWaveSegmentChanges();
                UpdateWaveChart();
                RaisePropertyChanged(nameof(CurrentWaveSegments));
                RaiseParameterDisplayProperties();
                RaiseWaveModeDisplayProperties();
            }
        }
    }

    public HwdLaserWaveSettings CurrentHwdWaveSettings
    {
        get => _currentHwdWaveSettings;
        private set
        {
            var oldSettings = _currentHwdWaveSettings;
            if (SetProperty(ref _currentHwdWaveSettings, value))
            {
                oldSettings.PropertyChanged -= OnCurrentHwdWaveSettingsChanged;
                CurrentHwdWaveSettings.PropertyChanged -= OnCurrentHwdWaveSettingsChanged;
                CurrentHwdWaveSettings.PropertyChanged += OnCurrentHwdWaveSettingsChanged;
                SubscribeWaveSegmentChanges();
                UpdateWaveChart();
                RaisePropertyChanged(nameof(CurrentWaveSegments));
                RaisePropertyChanged(nameof(CanEditHwdTrigger));
                RaisePropertyChanged(nameof(IsHwdFcwMode));
                RaisePropertyChanged(nameof(IsHwdSegmentMode));
                RaisePropertyChanged(nameof(ShowWaveSegmentEditor));
                RaiseParameterDisplayProperties();
            }
        }
    }

    public HwdLaserWaveSettings HwdParameterSnapshot
    {
        get => _hwdParameterSnapshot;
        private set
        {
            if (SetProperty(ref _hwdParameterSnapshot, value))
            {
                RaiseParameterDisplayProperties();
            }
        }
    }

    public LaserSystemSettings CurrentSystemSettings
    {
        get => _currentSystemSettings;
        private set
        {
            var oldSettings = _currentSystemSettings;
            if (SetProperty(ref _currentSystemSettings, value))
            {
                oldSettings.PropertyChanged -= OnCurrentSystemSettingsChanged;
                CurrentSystemSettings.PropertyChanged -= OnCurrentSystemSettingsChanged;
                CurrentSystemSettings.PropertyChanged += OnCurrentSystemSettingsChanged;
                RaiseSystemSettingsDisplayProperties();
            }
        }
    }

    public HwdLaserSystemSettings CurrentHwdSystemSettings
    {
        get => _currentHwdSystemSettings;
        private set => SetProperty(ref _currentHwdSystemSettings, value);
    }

    public LaserRealtimeStatus CurrentRealtimeStatus
    {
        get => _currentRealtimeStatus;
        private set
        {
            if (SetProperty(ref _currentRealtimeStatus, value))
            {
                RaiseRealtimeDisplayProperties();
            }
        }
    }

    public string NewDeviceName
    {
        get => _newDeviceName;
        set => SetProperty(ref _newDeviceName, value);
    }

    public string NewPortName
    {
        get => _newPortName;
        set => SetProperty(ref _newPortName, value);
    }

    public int NewBaudRate
    {
        get => _newBaudRate;
        set => SetProperty(ref _newBaudRate, value);
    }

    public LaserDeviceModel NewDeviceModel
    {
        get => _newDeviceModel;
        set
        {
            if (SetProperty(ref _newDeviceModel, value))
            {
                RaisePropertyChanged(nameof(IsNewDeviceHwd));
            }
        }
    }

    public bool IsNewDeviceHwd => NewDeviceModel == LaserDeviceModel.HWD;

    public HwdPowerScale NewHwdPowerScale
    {
        get => _newHwdPowerScale;
        set => SetProperty(ref _newHwdPowerScale, value);
    }

    public string OperationMessage
    {
        get => _operationMessage;
        private set => SetProperty(ref _operationMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsContinuousOutputEnabled
    {
        get => _isContinuousOutputEnabled;
        private set
        {
            if (SetProperty(ref _isContinuousOutputEnabled, value))
            {
                RaisePropertyChanged(nameof(ContinuousOutputText));
                RaisePropertyChanged(nameof(DisplayOutputStatus));
                RaisePropertyChanged(nameof(DisplayOutputStatusBrush));
            }
        }
    }

    public bool IsLaserLocked
    {
        get => _isLaserLocked;
        private set
        {
            if (SetProperty(ref _isLaserLocked, value))
            {
                RaisePropertyChanged(nameof(LaserLockText));
            }
        }
    }

    public bool IsRedLightEnabled
    {
        get => _isRedLightEnabled;
        private set
        {
            if (SetProperty(ref _isRedLightEnabled, value))
            {
                RaisePropertyChanged(nameof(RedLightText));
            }
        }
    }

    private void AddDevice()
    {
        var device = new LaserDeviceConfig
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(NewDeviceName) ? $"激光器{Devices.Count + 1}" : NewDeviceName.Trim(),
            PortName = NormalizePortName(NewPortName),
            BaudRate = NewBaudRate <= 0 ? 115200 : NewBaudRate,
            IsEnabled = true,
            Model = NewDeviceModel,
            DeviceAddress = 0x01,
            HwdPowerScale = NewHwdPowerScale
        };
        _laserDeviceService.AddOrUpdateDevice(device);
        var item = CreateDeviceItem(device);
        Devices.Add(item);
        PersistDevices();
        SelectedDevice = item;
        OperationMessage = $"已新增激光器：{item.Name}";
    }

    private void RemoveSelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        var removeTarget = SelectedDevice;
        _laserDeviceService.RemoveDevice(removeTarget.Id);
        Devices.Remove(removeTarget);
        PersistDevices();
        SelectedDevice = Devices.FirstOrDefault();
        OperationMessage = $"已删除激光器：{removeTarget.Name}";
    }

    private void SaveSelectedDevice()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        SelectedDevice.Name = string.IsNullOrWhiteSpace(NewDeviceName) ? SelectedDevice.Name : NewDeviceName.Trim();
        SelectedDevice.PortName = NormalizePortName(NewPortName);
        SelectedDevice.BaudRate = NewBaudRate <= 0 ? 115200 : NewBaudRate;
        SelectedDevice.Model = NewDeviceModel;
        SelectedDevice.DeviceAddress = 0x01;
        SelectedDevice.HwdPowerScale = NewHwdPowerScale;

        _laserDeviceService.AddOrUpdateDevice(SelectedDevice.ToConfig());
        PersistDevices();
        OperationMessage = $"已保存激光器配置：{SelectedDevice.Name}";
    }

    private void SaveWave()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            if (SelectedDevice.Model == LaserDeviceModel.HWD)
            {
                ApplyHwdWaveSettingsFromDevice(_laserDeviceService.WriteHwdWaveSettings(
                    SelectedDevice.Id,
                    CurrentHwdWaveSettings));
            }
            else
            {
                CurrentWaveSettings = _laserDeviceService.WriteWaveSettings(
                    SelectedDevice.Id,
                    CurrentWaveSettings);
            }
            SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            OperationMessage = $"已保存波形：{SelectedDevice.Name}";
        });
    }

    private void SwitchWave()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            if (SelectedDevice.Model == LaserDeviceModel.HWD)
            {
                var switchedSettings = _laserDeviceService.SwitchHwdWave(
                    SelectedDevice.Id,
                    CurrentHwdWaveSettings.WaveNumber);
                CurrentHwdWaveSettings.CurrentOutputWaveNumber = switchedSettings.CurrentOutputWaveNumber;
                HwdParameterSnapshot.CurrentOutputWaveNumber = switchedSettings.CurrentOutputWaveNumber;
                RaisePropertyChanged(nameof(DisplayHwdCurrentOutputWaveNumber));
            }
            else
            {
                CurrentWaveSettings = _laserDeviceService.SwitchWave(
                    SelectedDevice.Id,
                    CurrentWaveSettings.WaveNumber);
            }
            SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            var waveNumber = SelectedDevice.Model == LaserDeviceModel.HWD
                ? CurrentHwdWaveSettings.WaveNumber
                : CurrentWaveSettings.WaveNumber;
            OperationMessage = $"已切换到波形 {waveNumber}：{SelectedDevice.Name}";
        });
    }

    private void ToggleContinuousOutput()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            var nextState = !IsContinuousOutputEnabled;
            _laserDeviceService.SetContinuousOutput(SelectedDevice.Id, nextState);
            IsContinuousOutputEnabled = nextState;
            OperationMessage = $"{SelectedDevice.Name} {ContinuousOutputText}";
        });
    }

    private void PointWeld()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            _laserDeviceService.PointWeld(SelectedDevice.Id);
            OperationMessage = $"已执行点焊：{SelectedDevice.Name}";
        });
    }

    private void ToggleLaserLock()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            var nextState = !IsLaserLocked;
            _laserDeviceService.SetLaserLock(SelectedDevice.Id, nextState);
            IsLaserLocked = nextState;
            OperationMessage = $"{SelectedDevice.Name} {LaserLockText}";
        });
    }

    private void ToggleRedLight()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            var nextState = !IsRedLightEnabled;
            _laserDeviceService.SetRedLight(SelectedDevice.Id, nextState);
            IsRedLightEnabled = nextState;
            OperationMessage = $"{SelectedDevice.Name} {RedLightText}";
        });
    }

    private void ClearPointCount()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            _laserDeviceService.ClearPointCount(SelectedDevice.Id);
            OperationMessage = $"已清除出光点数：{SelectedDevice.Name}";
        });
    }

    private void ClearError()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            _laserDeviceService.ClearError(SelectedDevice.Id);
            OperationMessage = $"已清除错误：{SelectedDevice.Name}";
        });
    }

    private void SaveSystemSettings()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        RunLaserOperation(() =>
        {
            if (SelectedDevice.Model == LaserDeviceModel.HWD)
            {
                CurrentHwdSystemSettings = _laserDeviceService.WriteHwdSystemSettings(
                    SelectedDevice.Id,
                    CurrentHwdSystemSettings);
            }
            else
            {
                CurrentSystemSettings = _laserDeviceService.WriteSystemSettings(
                    SelectedDevice.Id,
                    CurrentSystemSettings);
            }
            SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            OperationMessage = $"已修改系统参数：{SelectedDevice.Name}";
        });
    }

    private bool CanOperateSelectedDevice()
    {
        return SelectedDevice is { IsConnected: true } && !IsBusy;
    }

    private void RunLaserOperation(Action operation)
    {
        IsBusy = true;
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            OperationMessage = $"激光器操作失败：{ex.Message}";
            if (SelectedDevice is not null)
            {
                SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            }
        }
        finally
        {
            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private void RefreshSelectedDeviceCachedRealtimeStatus()
    {
        if (SelectedDevice is not { } selectedDevice)
        {
            return;
        }

        selectedDevice.IsConnected = _laserDeviceService.IsConnected(selectedDevice.Id);
        if (!selectedDevice.IsConnected)
        {
            return;
        }

        var cachedStatus = _laserDeviceService.GetCachedRealtimeStatus(selectedDevice.Id);
        if (cachedStatus is null)
        {
            return;
        }

        CurrentRealtimeStatus = cachedStatus;
        IsContinuousOutputEnabled = cachedStatus.IsLaserOutputActive;
        IsLaserLocked = cachedStatus.SoftwareLockActive;
        IsRedLightEnabled = cachedStatus.IsRedLightActive;
    }

    private async Task RefreshSelectedDeviceRealtimeStatusAsync()
    {
        if (_isRefreshingRealtimeStatus || SelectedDevice is not { IsConnected: true } selectedDevice)
        {
            return;
        }

        if (selectedDevice.Model == LaserDeviceModel.HWD
            && DateTime.UtcNow - _lastHwdRealtimeRefresh < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _isRefreshingRealtimeStatus = true;
        try
        {
            var deviceId = selectedDevice.Id;
            var result = await Task.Run(() => new
            {
                Status = _laserDeviceService.ReadRealtimeStatus(deviceId),
            }).ConfigureAwait(true);
            if (SelectedDevice?.Id != deviceId)
            {
                return;
            }

            CurrentRealtimeStatus = result.Status;

            IsContinuousOutputEnabled = result.Status.IsLaserOutputActive;
            IsLaserLocked = result.Status.SoftwareLockActive;
            IsRedLightEnabled = result.Status.IsRedLightActive;
            selectedDevice.IsConnected = _laserDeviceService.IsConnected(deviceId);
            if (selectedDevice.Model == LaserDeviceModel.HWD)
            {
                _lastHwdRealtimeRefresh = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            if (SelectedDevice is not null)
            {
                SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            }

            OperationMessage = $"实时读取失败：{ex.Message}";
        }
        finally
        {
            _isRefreshingRealtimeStatus = false;
        }
    }

    private void StartHwdFastRealtimePolling()
    {
        if (Application.Current?.Dispatcher is null)
        {
            return;
        }

        _hwdFastPollingThread = new Thread(() =>
        {
            while (!_hwdFastPollingCancellation.IsCancellationRequested)
            {
                RefreshSelectedHwdFastRealtimeStatus();
                if (_hwdFastPollingCancellation.Token.WaitHandle.WaitOne(HwdFastPollingIntervalMilliseconds))
                {
                    break;
                }
            }
        })
        {
            IsBackground = true,
            Name = "HWD Fast Realtime Polling"
        };
        _hwdFastPollingThread.Start();
    }

    private void RefreshSelectedHwdFastRealtimeStatus()
    {
        var selectedDevice = SelectedDevice;
        if (selectedDevice is not { IsConnected: true, Model: LaserDeviceModel.HWD })
        {
            return;
        }

        try
        {
            var deviceId = selectedDevice.Id;
            var fastStatus = _laserDeviceService.ReadHwdFastRealtimeStatus(deviceId);
            void ApplyFastStatus()
            {
                if (SelectedDevice?.Id != deviceId)
                {
                    return;
                }

                var mergedStatus = CurrentRealtimeStatus.Clone();
                mergedStatus.LaserPower = fastStatus.LaserPower;
                mergedStatus.OutputPointCount = fastStatus.OutputPointCount;
                CurrentRealtimeStatus = mergedStatus;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(ApplyFastStatus, DispatcherPriority.Background);
            }
            else
            {
                ApplyFastStatus();
            }
        }
        catch
        {
            // The slower status loop owns connection and error reporting.
        }
    }

    private void LoadSelectedDeviceToEditor()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        NewDeviceName = SelectedDevice.Name;
        NewPortName = SelectedDevice.PortName;
        NewBaudRate = SelectedDevice.BaudRate;
        NewDeviceModel = SelectedDevice.Model;
        NewHwdPowerScale = SelectedDevice.HwdPowerScale;
    }

    private void LoadSelectedDeviceCachedSettings()
    {
        if (SelectedDevice is null)
        {
            CurrentWaveSettings = new LaserWaveSettings();
            CurrentHwdWaveSettings = new HwdLaserWaveSettings();
            HwdParameterSnapshot = new HwdLaserWaveSettings();
            CurrentSystemSettings = new LaserSystemSettings();
            CurrentHwdSystemSettings = new HwdLaserSystemSettings();
            CurrentRealtimeStatus = new LaserRealtimeStatus();
            OperationMessage = "请选择或新增激光器。";
            return;
        }

        SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
        if (SelectedDevice.Model == LaserDeviceModel.HWD)
        {
            var cachedSettings = _laserDeviceService.GetCachedHwdWaveSettings(SelectedDevice.Id)
                ?? new HwdLaserWaveSettings();
            CurrentHwdWaveSettings = cachedSettings;
            HwdParameterSnapshot = cachedSettings.Clone();
            CurrentHwdSystemSettings = _laserDeviceService.GetCachedHwdSystemSettings(SelectedDevice.Id)
                ?? new HwdLaserSystemSettings();
            CurrentSystemSettings = new LaserSystemSettings();
        }
        else
        {
            CurrentWaveSettings = _laserDeviceService.GetCachedWaveSettings(SelectedDevice.Id)
                ?? new LaserWaveSettings();
            CurrentSystemSettings = _laserDeviceService.GetCachedSystemSettings(SelectedDevice.Id)
                ?? new LaserSystemSettings();
            CurrentHwdSystemSettings = new HwdLaserSystemSettings();
        }
        CurrentRealtimeStatus = _laserDeviceService.GetCachedRealtimeStatus(SelectedDevice.Id) ?? new LaserRealtimeStatus();
        OperationMessage = $"当前激光器：{SelectedDevice.Name}";
    }

    private void PersistDevices()
    {
        _laserDeviceService.SaveDevices(Devices.Select(device => device.ToConfig()));
    }

    private LaserDeviceItemViewModel CreateDeviceItem(LaserDeviceConfig config)
    {
        return new LaserDeviceItemViewModel(config)
        {
            IsConnected = _laserDeviceService.IsConnected(config.Id)
        };
    }

    private void UpdateWaveChart()
    {
        _wavePoints.Clear();
        if (IsHwdFcwMode)
        {
            return;
        }

        foreach (var segment in CurrentWaveSegments)
        {
            _wavePoints.Add(new ObservablePoint(segment.Index, segment.Power));
        }
    }

    private void SubscribeWaveSegmentChanges()
    {
        foreach (var segment in CurrentWaveSettings.Segments.Concat(CurrentHwdWaveSettings.Segments))
        {
            segment.PropertyChanged -= OnWaveSegmentChanged;
            segment.PropertyChanged += OnWaveSegmentChanged;
        }
    }

    private void OnWaveSegmentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LaserWaveSegment.Power) || e.PropertyName == nameof(LaserWaveSegment.Time))
        {
            UpdateWaveChart();
        }
    }

    private void OnCurrentWaveSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (IsHwqSelected && e.PropertyName == nameof(LaserWaveSettings.WaveNumber))
        {
            RefreshWaveSettingsForChangedWaveNumber();
        }

        if (e.PropertyName == nameof(LaserWaveSettings.WaveModeIndex) && !_isApplyingWaveSettingsFromDevice)
        {
            ResetWaveSettingsForMode(CurrentWaveSettings.WaveModeIndex);
            return;
        }

        if (e.PropertyName == nameof(LaserWaveSettings.WaveNumber) ||
            e.PropertyName == nameof(LaserWaveSettings.WaveModeIndex) ||
            e.PropertyName == nameof(LaserWaveSettings.MaximumPeakPower) ||
            e.PropertyName == nameof(LaserWaveSettings.MaximumAveragePower) ||
            e.PropertyName == nameof(LaserWaveSettings.PresetLaserEnergy))
        {
            RaiseParameterDisplayProperties();
        }
    }

    private void ResetWaveSettingsForMode(int waveModeIndex)
    {
        var waveNumber = CurrentWaveSettings.WaveNumber;
        _isApplyingWaveSettingsFromDevice = true;
        try
        {
            CurrentWaveSettings = CreateDefaultWaveSettings(waveNumber, waveModeIndex);
        }
        finally
        {
            _isApplyingWaveSettingsFromDevice = false;
        }
    }

    private static LaserWaveSettings CreateDefaultWaveSettings(int waveNumber, int waveModeIndex)
    {
        return new LaserWaveSettings
        {
            WaveNumber = waveNumber,
            WaveModeIndex = waveModeIndex,
            OutputFrequency = 0,
            MaximumPeakPower = 0,
            MaximumAveragePower = 0,
            AverageFrequency = 0,
            EnergyAlarmUpper = 0,
            EnergyAlarmLower = 0,
            PresetLaserEnergy = 0,
            MaxOutputFrequency = 0
        };
    }

    private void RefreshWaveSettingsForChangedWaveNumber()
    {
        if (_isApplyingWaveSettingsFromDevice || SelectedDevice is not { IsConnected: true } selectedDevice)
        {
            return;
        }

        _isApplyingWaveSettingsFromDevice = true;
        try
        {
            var deviceId = selectedDevice.Id;
            CurrentWaveSettings = _laserDeviceService.ReadWaveNumberSettings(deviceId, CurrentWaveSettings.WaveNumber);
            if (SelectedDevice?.Id != deviceId)
            {
                return;
            }
            selectedDevice.IsConnected = _laserDeviceService.IsConnected(deviceId);
        }
        catch (Exception ex)
        {
            if (SelectedDevice is not null)
            {
                SelectedDevice.IsConnected = _laserDeviceService.IsConnected(SelectedDevice.Id);
            }

            OperationMessage = $"读取切换波形失败{ex.Message}";
        }
        finally
        {
            _isApplyingWaveSettingsFromDevice = false;
        }
        UpdateWaveChart();
    }

    private void OnCurrentHwdWaveSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HwdLaserWaveSettings.WaveNumber))
        {
            RefreshHwdWaveSettingsForChangedWaveNumber();
        }

        if (e.PropertyName == nameof(HwdLaserWaveSettings.EmitMode)
            && !_isApplyingHwdWaveSettingsFromDevice)
        {
            ResetHwdWaveSettingsForMode(CurrentHwdWaveSettings.EmitMode);
            return;
        }

        if (e.PropertyName == nameof(HwdLaserWaveSettings.WaveNumber)
            || e.PropertyName == nameof(HwdLaserWaveSettings.EmitMode)
            || e.PropertyName == nameof(HwdLaserWaveSettings.TriggerMode)
            || e.PropertyName == nameof(HwdLaserWaveSettings.AveragePower)
            || e.PropertyName == nameof(HwdLaserWaveSettings.SinglePointEnergy))
        {
            RaiseParameterDisplayProperties();
        }
    }

    private void RefreshHwdWaveSettingsForChangedWaveNumber()
    {
        if (_isApplyingHwdWaveSettingsFromDevice
            || SelectedDevice is not { IsConnected: true, Model: LaserDeviceModel.HWD } selectedDevice)
        {
            return;
        }

        _isApplyingHwdWaveSettingsFromDevice = true;
        try
        {
            var deviceId = selectedDevice.Id;
            ApplyHwdWaveSettingsFromDevice(_laserDeviceService.ReadHwdWaveNumberSettings(
                deviceId,
                CurrentHwdWaveSettings.WaveNumber));
            selectedDevice.IsConnected = _laserDeviceService.IsConnected(deviceId);
        }
        catch (Exception ex)
        {
            selectedDevice.IsConnected = _laserDeviceService.IsConnected(selectedDevice.Id);
            OperationMessage = $"读取 HWD 波形失败：{ex.Message}";
        }
        finally
        {
            _isApplyingHwdWaveSettingsFromDevice = false;
        }
    }

    private void ResetHwdWaveSettingsForMode(HwdEmitMode emitMode)
    {
        var waveNumber = CurrentHwdWaveSettings.WaveNumber;
        var currentOutputWaveNumber = CurrentHwdWaveSettings.CurrentOutputWaveNumber;
        _isApplyingHwdWaveSettingsFromDevice = true;
        try
        {
            CurrentHwdWaveSettings = new HwdLaserWaveSettings
            {
                WaveNumber = waveNumber,
                CurrentOutputWaveNumber = currentOutputWaveNumber,
                EmitMode = emitMode,
                TriggerMode = HwdTriggerMode.电平出光
            };
        }
        finally
        {
            _isApplyingHwdWaveSettingsFromDevice = false;
        }
    }

    private void ApplyHwdWaveSettingsFromDevice(HwdLaserWaveSettings settings)
    {
        HwdParameterSnapshot = settings.Clone();
        CurrentHwdWaveSettings = settings;
    }

    private void OnCurrentSystemSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LaserSystemSettings.LaserTriggerModeIndex))
        {
            RaisePropertyChanged(nameof(DisplayTriggerMode));
        }
    }

    private void RaiseParameterDisplayProperties()
    {
        RaisePropertyChanged(nameof(DisplayLaserPower));
        RaisePropertyChanged(nameof(DisplayWaveNumber));
        RaisePropertyChanged(nameof(DisplayWaveMode));
        RaisePropertyChanged(nameof(DisplayTriggerMode));
        RaisePropertyChanged(nameof(DisplaySinglePointEnergy));
        RaisePropertyChanged(nameof(DisplayOutputPointCount));
        RaisePropertyChanged(nameof(DisplayAveragePower));
        RaisePropertyChanged(nameof(DisplayAnalogVoltage));
        RaisePropertyChanged(nameof(DisplayHwdPulseInterval));
        RaisePropertyChanged(nameof(DisplayHwdModulationPeriod));
        RaisePropertyChanged(nameof(DisplayHwdOutputRatio));
        RaisePropertyChanged(nameof(DisplayHwdCurrentOutputWaveNumber));
    }

    private void RaiseWaveModeDisplayProperties()
    {
        RaisePropertyChanged(nameof(WaveFrequencyLabel));
        RaisePropertyChanged(nameof(PresetEnergyLabel));
        RaisePropertyChanged(nameof(MaxOutputFrequencyLabel));
        RaisePropertyChanged(nameof(AlarmRangeLabel));
        RaisePropertyChanged(nameof(WaveSegmentTimeHeader));
        RaisePropertyChanged(nameof(WaveSegmentPowerHeader));
    }

    private void RaiseRealtimeDisplayProperties()
    {
        RaisePropertyChanged(nameof(DisplayLaserPower));
        RaisePropertyChanged(nameof(DisplayRealtimeLaserPower));
        RaisePropertyChanged(nameof(DisplayOutputPointCount));
        RaisePropertyChanged(nameof(DisplayAnalogVoltage));
        RaisePropertyChanged(nameof(DisplayOutputStatus));
        RaisePropertyChanged(nameof(DisplayAlarmStatus));
        RaisePropertyChanged(nameof(DisplayOutputStatusBrush));
        RaisePropertyChanged(nameof(DisplayAlarmStatusBrush));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _realtimeTimer.Stop();
        _hwdFastPollingCancellation.Cancel();
    }

    public void Destroy()
    {
        Dispose();
    }

    private void RaiseSystemSettingsDisplayProperties()
    {
        RaisePropertyChanged(nameof(DisplayTriggerMode));
    }

    private void RaiseCommandStates()
    {
        AddDeviceCommand.RaiseCanExecuteChanged();
        RemoveSelectedDeviceCommand.RaiseCanExecuteChanged();
        SaveSelectedDeviceCommand.RaiseCanExecuteChanged();
        SaveWaveCommand.RaiseCanExecuteChanged();
        SwitchWaveCommand.RaiseCanExecuteChanged();
        ToggleContinuousOutputCommand.RaiseCanExecuteChanged();
        PointWeldCommand.RaiseCanExecuteChanged();
        ToggleLaserLockCommand.RaiseCanExecuteChanged();
        ToggleRedLightCommand.RaiseCanExecuteChanged();
        ClearPointCountCommand.RaiseCanExecuteChanged();
        ClearErrorCommand.RaiseCanExecuteChanged();
        SaveSystemSettingsCommand.RaiseCanExecuteChanged();
    }

    private static string NormalizePortName(string portName)
    {
        return string.IsNullOrWhiteSpace(portName) ? "COM1" : portName.Trim().ToUpperInvariant();
    }
}

public sealed class LaserDeviceItemViewModel : BindableBase
{
    private string _name;
    private string _portName;
    private int _baudRate;
    private bool _isEnabled;
    private bool _isConnected;
    private LaserDeviceModel _model;
    private byte _deviceAddress;
    private HwdPowerScale _hwdPowerScale;

    public LaserDeviceItemViewModel(LaserDeviceConfig config)
    {
        Id = config.Id;
        _name = config.Name;
        _portName = config.PortName;
        _baudRate = config.BaudRate;
        _isEnabled = config.IsEnabled;
        _model = config.Model;
        _deviceAddress = config.DeviceAddress;
        _hwdPowerScale = config.HwdPowerScale;
    }

    public Guid Id { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public LaserDeviceModel Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public byte DeviceAddress
    {
        get => _deviceAddress;
        set => SetProperty(ref _deviceAddress, value);
    }

    public HwdPowerScale HwdPowerScale
    {
        get => _hwdPowerScale;
        set => SetProperty(ref _hwdPowerScale, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string ConnectionState => IsConnected ? "已连接" : "未连接";

    public LaserDeviceConfig ToConfig()
    {
        return new LaserDeviceConfig
        {
            Id = Id,
            Name = Name,
            PortName = PortName,
            BaudRate = BaudRate,
            IsEnabled = IsEnabled,
            Model = Model,
            DeviceAddress = DeviceAddress,
            HwdPowerScale = HwdPowerScale
        };
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.PropertyName == nameof(IsConnected))
        {
            RaisePropertyChanged(nameof(ConnectionState));
        }
    }
}
