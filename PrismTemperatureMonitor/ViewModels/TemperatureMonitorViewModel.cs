using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;
using SkiaSharp;

namespace PrismTemperatureMonitor.ViewModels;

public sealed class TemperatureMonitorViewModel : BindableBase
{
    private const int DefaultVisiblePointCount = 120;
    private const int SampleIntervalMilliseconds = 100;

    public const int MaxRenderedPointCount = 1_000;
    public static readonly PlcAddress PlcAcquisitionStartAddress = new(16, 102, PlcValueType.Bool, 1);
    public static readonly PlcAddress PlcTemperatureAddress = new(8, 32, PlcValueType.Float);
    public static readonly PlcAddress PlcTemperatureResultAddress = new(16, 102, PlcValueType.Bool, 4);
    public static readonly PlcAddress PlcRecipeIndexAddress = new(16, 0, PlcValueType.Int);
    public static readonly PlcAddress PlcLaserRealtimePowerAddress = new(16, 2, PlcValueType.Float);
    public static readonly PlcAddress PlcMesRecipeRecordTriggerAddress = new(16, 102, PlcValueType.Bool, 3);

    private readonly ObservableCollection<ObservablePoint> _temperatureValues = [];
    private readonly TemperatureChartBuffer _chartBuffer;
    private readonly ITemperatureHistoryWriter _historyWriter;
    private readonly IMesRecipeRecordWriter _mesRecipeRecordWriter;
    private readonly ILaserDeviceService _laserDeviceService;
    private readonly IPlcService _plcService;
    private readonly IRecipeConfigStore _recipeConfigStore;
    private readonly DispatcherTimer _sampleTimer;
    private readonly DispatcherTimer _laserDeviceListTimer;
    private readonly DispatcherTimer _laserSnapshotTimer;
    private readonly Random _random = new(20260603);
    private DateTime _axisStartTime;
    private DateTime _latestSampleTime;
    private int _sampleIndex;
    private int _sampleCount;
    private bool _isCollecting;
    private double _currentTemperature;
    private double _temperatureUpperLimit;
    private double _temperatureLowerLimit;
    private string _temperatureResult = "等待采集";
    private string _collectionState = "未采集";
    private Brush _collectionStateBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
    private Brush _temperatureResultBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
    private LaserDeviceConfig? _selectedLaserDevice;
    private LaserDisplaySnapshot? _currentLaserSnapshot;
    private TemperatureRecipeOption? _selectedTemperatureRecipe;
    private bool _lastPlcAcquisitionSignal;
    private bool _suppressRecipeIndexWrite;
    private Guid? _lastSyncedLaserPowerDeviceId;
    private double? _lastSyncedLaserPower;
    private string _laserSelectionMessage = "暂无已连接激光器";

    public TemperatureMonitorViewModel(
        ITemperatureHistoryWriter historyWriter,
        ILaserDeviceService laserDeviceService,
        IPlcService plcService,
        IRecipeConfigStore recipeConfigStore,
        IMesRecipeRecordWriter mesRecipeRecordWriter)
    {
        _historyWriter = historyWriter;
        _mesRecipeRecordWriter = mesRecipeRecordWriter;
        _laserDeviceService = laserDeviceService;
        _plcService = plcService;
        _recipeConfigStore = recipeConfigStore;
        _recipeConfigStore.SettingsSaved += OnRecipeSettingsSaved;
        _chartBuffer = new TemperatureChartBuffer(_temperatureValues, MaxRenderedPointCount);
        _axisStartTime = DateTime.Now;
        _latestSampleTime = _axisStartTime;
        LoadTemperatureRecipes();
        ConnectPlcOnStartup();

        Series =
        [
            new LineSeries<ObservablePoint>
            {
                Name = "温区",
                Values = _temperatureValues,
                GeometrySize = 0,
                LineSmoothness = 0.85,
                Stroke = new SolidColorPaint(SKColor.Parse("#0EA5E9"), 3),
                Fill = null
            }
        ];

        XAxes =
        [
            new Axis
            {
                Name = "时间",
                Labeler = value => DateTime.FromOADate(value).ToString("HH:mm:ss"),
                NamePaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0")),
                MinLimit = _axisStartTime.ToOADate(),
                MaxLimit = GetDefaultAxisMaxLimit()
            }
        ];

        YAxes =
        [
            new Axis
            {
                Name = "温度 / °C",
                NamePaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#64748B")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E2E8F0")),
                MinLimit = 0,
                MaxLimit = 260
            }
        ];

        StartCollectingCommand = new DelegateCommand(StartCollecting, () => !IsCollecting)
            .ObservesProperty(() => IsCollecting);
        StopCollectingCommand = new DelegateCommand(StopCollecting, () => IsCollecting)
            .ObservesProperty(() => IsCollecting);
        ClearChartCommand = new DelegateCommand(ClearChart);

        _sampleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SampleIntervalMilliseconds)
        };
        _sampleTimer.Tick += (_, _) => PollPlc();
        _laserSnapshotTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _laserSnapshotTimer.Tick += (_, _) => RefreshSelectedLaserSnapshot();

        _laserDeviceListTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _laserDeviceListTimer.Tick += (_, _) => RefreshConnectedLaserDevices();

        RefreshConnectedLaserDevices();
        _laserSnapshotTimer.Start();
        _laserDeviceListTimer.Start();
        _sampleTimer.Start();
    }

    public ISeries[] Series { get; }

    public Axis[] XAxes { get; }

    public Axis[] YAxes { get; }

    public DelegateCommand StartCollectingCommand { get; }

    public DelegateCommand StopCollectingCommand { get; }

    public DelegateCommand ClearChartCommand { get; }

    public ObservableCollection<LaserDeviceConfig> ConnectedLaserDevices { get; } = [];

    public ObservableCollection<TemperatureRecipeOption> TemperatureRecipes { get; } = [];

    public TemperatureRecipeOption? SelectedTemperatureRecipe
    {
        get => _selectedTemperatureRecipe;
        set
        {
            if (!SetProperty(ref _selectedTemperatureRecipe, value) || value is null || _suppressRecipeIndexWrite)
            {
                return;
            }

            try
            {
                _plcService.WriteInt(PlcRecipeIndexAddress, value.Index+1);
            }
            catch
            {
                CollectionState = "PLC配方下发失败";
                CollectionStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }
    }

    public LaserDeviceConfig? SelectedLaserDevice
    {
        get => _selectedLaserDevice;
        set
        {
            if (!SetProperty(ref _selectedLaserDevice, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(IsHwqLaserSelected));
            RaisePropertyChanged(nameof(IsHwdLaserSelected));
            RefreshSelectedLaserSnapshot();
        }
    }

    public LaserDisplaySnapshot? CurrentLaserSnapshot
    {
        get => _currentLaserSnapshot;
        private set => SetProperty(ref _currentLaserSnapshot, value);
    }

    public bool IsHwqLaserSelected => SelectedLaserDevice?.Model == LaserDeviceModel.HWQ;

    public bool IsHwdLaserSelected => SelectedLaserDevice?.Model == LaserDeviceModel.HWD;

    public string LaserSelectionMessage
    {
        get => _laserSelectionMessage;
        private set => SetProperty(ref _laserSelectionMessage, value);
    }

    public bool IsCollecting
    {
        get => _isCollecting;
        private set => SetProperty(ref _isCollecting, value);
    }

    public string ConnectionState => "设备在线";

    public string AlarmState => "正常";

    public Brush AlarmStateBrush { get; } = new SolidColorBrush(Color.FromRgb(34, 197, 94));

    public string CollectionState
    {
        get => _collectionState;
        private set => SetProperty(ref _collectionState, value);
    }

    public Brush CollectionStateBrush
    {
        get => _collectionStateBrush;
        private set => SetProperty(ref _collectionStateBrush, value);
    }

    public double CurrentTemperature
    {
        get => _currentTemperature;
        private set => SetProperty(ref _currentTemperature, value);
    }

    public double TargetTemperature { get; } = 180;

    public double TemperatureUpperLimit
    {
        get => _temperatureUpperLimit;
        private set => SetProperty(ref _temperatureUpperLimit, value);
    }

    public double TemperatureLowerLimit
    {
        get => _temperatureLowerLimit;
        private set => SetProperty(ref _temperatureLowerLimit, value);
    }

    public string TemperatureResult
    {
        get => _temperatureResult;
        private set => SetProperty(ref _temperatureResult, value);
    }

    public Brush TemperatureResultBrush
    {
        get => _temperatureResultBrush;
        private set => SetProperty(ref _temperatureResultBrush, value);
    }

    public int SampleCount => _sampleCount;

    public int RenderedPointCount => _chartBuffer.RenderedPointCount;

    public void AppendSampleForTest()
    {
        if (!IsCollecting)
        {
            return;
        }

        AppendTemperatureSample(GenerateTemperature());
    }

    public void PollPlcForTest()
    {
        PollPlc();
    }

    public void RefreshLaserDevicesForTest()
    {
        RefreshConnectedLaserDevices();
    }

    public void RefreshLaserSnapshotForTest()
    {
        RefreshSelectedLaserSnapshot();
    }

    private void RefreshConnectedLaserDevices()
    {
        var connectedDevices = _laserDeviceService.GetDevices()
            .Where(device => device.IsEnabled && _laserDeviceService.IsConnected(device.Id))
            .ToArray();
        var selectedDeviceId = SelectedLaserDevice?.Id;
        var listChanged = ConnectedLaserDevices.Count != connectedDevices.Length ||
                          ConnectedLaserDevices
                              .Select(device => device.Id)
                              .SequenceEqual(connectedDevices.Select(device => device.Id)) == false;

        if (listChanged)
        {
            ConnectedLaserDevices.Clear();
            foreach (var device in connectedDevices)
            {
                ConnectedLaserDevices.Add(device);
            }
        }

        var nextSelection = selectedDeviceId.HasValue
            ? ConnectedLaserDevices.FirstOrDefault(device => device.Id == selectedDeviceId.Value)
            : null;
        SelectedLaserDevice = nextSelection ?? ConnectedLaserDevices.FirstOrDefault();

        if (SelectedLaserDevice is null)
        {
            CurrentLaserSnapshot = null;
            LaserSelectionMessage = "暂无已连接激光器";
        }
    }

    private void RefreshSelectedLaserSnapshot()
    {
        if (SelectedLaserDevice is null)
        {
            CurrentLaserSnapshot = null;
            LaserSelectionMessage = "暂无已连接激光器";
            return;
        }

        CurrentLaserSnapshot = _laserDeviceService.GetCachedDisplaySnapshot(SelectedLaserDevice.Id);
        LaserSelectionMessage = CurrentLaserSnapshot is null
            ? "等待激光器参数"
            : $"当前激光器：{SelectedLaserDevice.Name}";
        SyncLaserRealtimePowerToPlc(CurrentLaserSnapshot);
    }

    private void ConnectPlcOnStartup()
    {
        try
        {
            _plcService.Connect();
        }
        catch
        {
            CollectionState = "PLC连接失败";
            CollectionStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void SyncLaserRealtimePowerToPlc(LaserDisplaySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            _lastSyncedLaserPowerDeviceId = null;
            _lastSyncedLaserPower = null;
            return;
        }

        if (_lastSyncedLaserPowerDeviceId == snapshot.DeviceId &&
            _lastSyncedLaserPower.HasValue &&
            Math.Abs(_lastSyncedLaserPower.Value - snapshot.RealtimePower) < 0.0001)
        {
            return;
        }

        try
        {
            _plcService.WriteFloat(PlcLaserRealtimePowerAddress, (float)snapshot.RealtimePower);
            _lastSyncedLaserPowerDeviceId = snapshot.DeviceId;
            _lastSyncedLaserPower = snapshot.RealtimePower;
        }
        catch
        {
            CollectionState = "激光功率写入PLC失败";
            CollectionStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void PollPlc()
    {
        try
        {
            if (!_plcService.IsConnected)
            {
                CollectionState = "PLC未连接";
                CollectionStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                return;
            }
            SaveMesRecipeRecordIfRequested();

            var acquisitionSignal = _plcService.ReadBool(PlcAcquisitionStartAddress);
            if (acquisitionSignal && !_lastPlcAcquisitionSignal)
            {
                ResetAcquisitionData();
                IsCollecting = true;
                CollectionState = "采集中";
                CollectionStateBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            }

            if (acquisitionSignal)
            {
                AppendTemperatureSample(_plcService.ReadFloat(PlcTemperatureAddress));
            }
            else if (_lastPlcAcquisitionSignal)
            {
                StopCollecting();
                UpdateTemperatureResultFromPlc(_plcService.ReadBool(PlcTemperatureResultAddress));
            }

            _lastPlcAcquisitionSignal = acquisitionSignal;
        }
        catch
        {
            CollectionState = "PLC读取异常";
            CollectionStateBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
    }

    private void StartCollecting()
    {
        ResetAcquisitionData();
        IsCollecting = true;
        CollectionState = "采集中";
        CollectionStateBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
    }

    private void StopCollecting()
    {
        IsCollecting = false;
        CollectionState = "已停止";
        CollectionStateBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
    }

    private void ClearChart()
    {
        ResetAcquisitionData();
    }

    private void ResetAcquisitionData()
    {
        _chartBuffer.Clear();
        _axisStartTime = DateTime.Now;
        _latestSampleTime = _axisStartTime;
        _sampleIndex = 0;
        _sampleCount = 0;
        CurrentTemperature = 0;
        TemperatureUpperLimit = 0;
        TemperatureLowerLimit = 0;
        TemperatureResult = "等待采集";
        TemperatureResultBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        RaisePropertyChanged(nameof(SampleCount));
        RaisePropertyChanged(nameof(RenderedPointCount));
        ResetAxisWindow();
    }

    private void AppendTemperatureSample(double temperature)
    {
        if (!IsCollecting)
        {
            return;
        }

        _latestSampleTime = DateTime.Now;
        var sample = new TemperatureSample(_latestSampleTime, _sampleIndex, temperature);

        CurrentTemperature = temperature;
        TemperatureUpperLimit = _sampleIndex == 0 ? temperature : Math.Max(TemperatureUpperLimit, temperature);
        TemperatureLowerLimit = _sampleIndex == 0 ? temperature : Math.Min(TemperatureLowerLimit, temperature);
        UpdateTemperatureResult(temperature);

        _historyWriter.Enqueue(sample);
        _chartBuffer.Add(sample);

        _sampleIndex++;
        _sampleCount++;
        UpdateAxisWindow();

        RaisePropertyChanged(nameof(SampleCount));
        RaisePropertyChanged(nameof(RenderedPointCount));
    }

    private double GenerateTemperature()
    {
        var noise = (_random.NextDouble() - 0.5) * 1.8;
        var value = 64 + (_sampleIndex * 0.085) + (Math.Sin(_sampleIndex / 8.0) * 8.5) + noise;
        return Math.Round(Math.Min(value, TargetTemperature + 12), 1);
    }

    private void UpdateTemperatureResult(double temperature)
    {
        if (temperature < 50)
        {
            TemperatureResult = "温度偏低";
            TemperatureResultBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            return;
        }

        if (temperature > 200)
        {
            TemperatureResult = "温度偏高";
            TemperatureResultBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            return;
        }

        TemperatureResult = "温度正常";
        TemperatureResultBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
    }

    private void UpdateTemperatureResultFromPlc(bool isOk)
    {
        TemperatureResult = isOk ? "OK" : "NG";
        TemperatureResultBrush = new SolidColorBrush(isOk
            ? Color.FromRgb(34, 197, 94)
            : Color.FromRgb(239, 68, 68));
    }

    private void LoadTemperatureRecipes()
    {
        RefreshTemperatureRecipes(LoadSavedRecipeSettings());
    }

    private void OnRecipeSettingsSaved(object? sender, RecipeSettings settings)
    {
        RefreshTemperatureRecipes(settings);
    }

    private void RefreshTemperatureRecipes(RecipeSettings settings)
    {
        var selectedCode = SelectedTemperatureRecipe?.Code;
        var savedRecipes = BuildRecipeMetadataLookup(settings);

        _suppressRecipeIndexWrite = true;
        try
        {
            TemperatureRecipes.Clear();
            for (var index = 0; index < 16; index++)
            {
                var code = $"R{index + 1:000}";
                var defaultName = index switch
                {
                    0 => "150H-B001",
                    1 => "150H-B002",
                    2 => "180H-C001",
                    _ => $"厚片配方-{index + 1:00}"
                };
                var name = savedRecipes.TryGetValue(code, out var savedRecipe) &&
                           string.IsNullOrWhiteSpace(savedRecipe.Name) == false
                    ? savedRecipe.Name
                    : defaultName;

                TemperatureRecipes.Add(new TemperatureRecipeOption(index, code, name));
            }

            SelectedTemperatureRecipe = selectedCode is null
                ? TemperatureRecipes.FirstOrDefault()
                : TemperatureRecipes.FirstOrDefault(recipe => recipe.Code == selectedCode) ?? TemperatureRecipes.FirstOrDefault();
        }
        finally
        {
            _suppressRecipeIndexWrite = false;
        }
    }

    private void SaveMesRecipeRecordIfRequested()
    {
        if (!_plcService.ReadBool(PlcMesRecipeRecordTriggerAddress))
        {
            return;
        }

        _plcService.WriteBool(PlcMesRecipeRecordTriggerAddress, false);
        _mesRecipeRecordWriter.Append(ReadMesRecipeRecordFromPlc());
    }

    private MesRecipeRecord ReadMesRecipeRecordFromPlc()
    {
        var record = new MesRecipeRecord
        {
            Timestamp = DateTime.Now,
            YAbsolutePositionSpeed = _plcService.ReadFloat(new PlcAddress(16, 130, PlcValueType.Float)),
            ZAbsolutePositionSpeed = _plcService.ReadFloat(new PlcAddress(16, 134, PlcValueType.Float)),
            WeldPassCount = _plcService.ReadInt(new PlcAddress(16, 138, PlcValueType.Int))
        };

        for (var index = 0; index < 6; index++)
        {
            record.WeldPasses.Add(ReadMesWeldPassFromPlc(index));
        }

        return record;
    }

    private MesWeldPassRecord ReadMesWeldPassFromPlc(int index)
    {
        var weldNumber = index + 1;
        var actualPowerOffset = 106 + (index * 4);
        var weldBaseOffset = 140 + (index * 42);
        return new MesWeldPassRecord
        {
            Index = weldNumber,
            ActualPower = _plcService.ReadInt(new PlcAddress(16, actualPowerOffset, PlcValueType.DInt)),
            WaveNumber = _plcService.ReadInt(new PlcAddress(16, weldBaseOffset, PlcValueType.Int)),
            RSpeed = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 2, PlcValueType.Float)),
            YPosition = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 6, PlcValueType.Float)),
            ZPosition = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 10, PlcValueType.Float)),
            RPreAngle = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 14, PlcValueType.Float)),
            RPosition = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 18, PlcValueType.Float)),
            RPostAngle = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 22, PlcValueType.Float)),
            TemperatureUpperLimit = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 26, PlcValueType.Float)),
            TemperatureLowerLimit = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 30, PlcValueType.Float)),
            LaserPowerUpperLimit = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 34, PlcValueType.Float)),
            LaserPowerLowerLimit = _plcService.ReadFloat(new PlcAddress(16, weldBaseOffset + 38, PlcValueType.Float))
        };
    }

    private RecipeSettings LoadSavedRecipeSettings()
    {
        try
        {
            return _recipeConfigStore.Load();
        }
        catch
        {
            return new RecipeSettings();
        }
    }

    private static Dictionary<string, RecipeMetadata> BuildRecipeMetadataLookup(RecipeSettings settings)
    {
        return settings
            .Recipes
            .Where(recipe => string.IsNullOrWhiteSpace(recipe.Code) == false)
            .GroupBy(recipe => recipe.Code)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    private void UpdateAxisWindow()
    {
        XAxes[0].MinLimit = _axisStartTime.ToOADate();
        XAxes[0].MaxLimit = Math.Max(GetDefaultAxisMaxLimit(), _latestSampleTime.ToOADate());
    }

    private void ResetAxisWindow()
    {
        XAxes[0].MinLimit = _axisStartTime.ToOADate();
        XAxes[0].MaxLimit = GetDefaultAxisMaxLimit();
    }

    private double GetDefaultAxisMaxLimit()
    {
        var defaultWindow = TimeSpan.FromMilliseconds(DefaultVisiblePointCount * SampleIntervalMilliseconds);
        return _axisStartTime.Add(defaultWindow).ToOADate();
    }
}

public sealed record TemperatureRecipeOption(int Index, string Code, string Name);

