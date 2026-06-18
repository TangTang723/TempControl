using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class LaserDeviceService : ILaserDeviceService, IDisposable
{
    private readonly ILaserSerialPortFactory _serialPortFactory;
    private readonly ILaserDeviceConfigStore? _configStore;
    private readonly List<LaserDeviceConfig> _devices = [];
    private readonly Dictionary<Guid, ILaserDeviceSession> _sessions = [];

    public LaserDeviceService(ILaserSerialPortFactory serialPortFactory, ILaserDeviceConfigStore? configStore = null)
    {
        _serialPortFactory = serialPortFactory;
        _configStore = configStore;
        if (_configStore is not null)
        {
            _devices.AddRange(_configStore.Load().Select(device => device.Clone()));
        }
    }

    public IReadOnlyList<LaserDeviceConfig> GetDevices()
    {
        return _devices.Select(device => device.Clone()).ToArray();
    }

    public void SaveDevices(IEnumerable<LaserDeviceConfig> devices)
    {
        _devices.Clear();
        _devices.AddRange(devices.Select(device => device.Clone()));
        _configStore?.Save(_devices);
    }

    public void AddOrUpdateDevice(LaserDeviceConfig device)
    {
        var existingIndex = _devices.FindIndex(item => item.Id == device.Id);
        if (existingIndex >= 0)
        {
            _devices[existingIndex] = device.Clone();
        }
        else
        {
            _devices.Add(device.Clone());
        }

        if (_sessions.TryGetValue(device.Id, out var session))
        {
            if (session.Config.Model != device.Model)
            {
                session.Dispose();
                _sessions.Remove(device.Id);
            }
            else
            {
                session.UpdateConfig(device);
            }
        }
    }

    public void RemoveDevice(Guid deviceId)
    {
        _devices.RemoveAll(device => device.Id == deviceId);
        if (_sessions.Remove(deviceId, out var session))
        {
            session.Dispose();
        }
    }

    public void Connect(Guid deviceId)
    {
        GetSession(deviceId).Connect();
    }

    public void Disconnect(Guid deviceId)
    {
        if (_sessions.TryGetValue(deviceId, out var session))
        {
            session.Disconnect();
        }
    }

    public bool IsConnected(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session) && session.IsConnected;
    }

    public LaserDeviceCapabilities GetCapabilities(Guid deviceId)
    {
        return GetDevice(deviceId).Model == LaserDeviceModel.HWD
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
        foreach (var device in _devices.Where(device => device.IsEnabled).ToArray())
        {
            var session = GetSession(device.Id);
            try
            {
                session.Connect();
                if (session is HwdLaserDeviceSession hwdSession)
                {
                    hwdSession.ReadWaveSettings();
                    hwdSession.ReadSystemSettings();
                }
                else
                {
                    var hwqSession = (LaserDeviceSession)session;
                    hwqSession.ReadWaveSettings();
                    hwqSession.ReadSystemSettings();
                }
            }
            catch
            {
                session.Disconnect();
            }
        }
    }

    public LaserWaveSettings? GetCachedWaveSettings(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session) && session is LaserDeviceSession hwqSession
            ? hwqSession.CachedWaveSettings?.Clone()
            : null;
    }

    public LaserWaveSettings ReadWaveSettings(Guid deviceId)
    {
        return GetHwqSession(deviceId).ReadWaveSettings();
    }
    public LaserWaveSettings ReadWaveNumberSettings(Guid deviceId,int Number)
    {
        return GetHwqSession(deviceId).ReadWaveNumberSettings(Number);
    }
    public LaserWaveSettings WriteWaveSettings(Guid deviceId, LaserWaveSettings settings)
    {
        return GetHwqSession(deviceId).WriteWaveSettings(settings);
    }

    public LaserWaveSettings SwitchWave(Guid deviceId, int waveNumber)
    {
        return GetHwqSession(deviceId).SwitchWave(waveNumber);
    }

    public HwdLaserWaveSettings? GetCachedHwdWaveSettings(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session) && session is HwdLaserDeviceSession hwdSession
            ? hwdSession.CachedWaveSettings?.Clone()
            : null;
    }

    public HwdLaserWaveSettings ReadHwdWaveSettings(Guid deviceId)
    {
        return GetHwdSession(deviceId).ReadWaveSettings();
    }

    public HwdLaserWaveSettings ReadHwdWaveNumberSettings(Guid deviceId, int waveNumber)
    {
        return GetHwdSession(deviceId).ReadWaveNumberSettings(waveNumber);
    }

    public HwdLaserWaveSettings WriteHwdWaveSettings(Guid deviceId, HwdLaserWaveSettings settings)
    {
        return GetHwdSession(deviceId).WriteWaveSettings(settings);
    }

    public HwdLaserWaveSettings SwitchHwdWave(Guid deviceId, int waveNumber)
    {
        return GetHwdSession(deviceId).SwitchWave(waveNumber);
    }

    public HwdLaserSystemSettings? GetCachedHwdSystemSettings(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session) && session is HwdLaserDeviceSession hwdSession
            ? hwdSession.CachedSystemSettings?.Clone()
            : null;
    }

    public HwdLaserSystemSettings ReadHwdSystemSettings(Guid deviceId)
    {
        return GetHwdSession(deviceId).ReadSystemSettings();
    }

    public HwdLaserSystemSettings WriteHwdSystemSettings(
        Guid deviceId,
        HwdLaserSystemSettings settings)
    {
        return GetHwdSession(deviceId).WriteSystemSettings(settings);
    }

    public LaserSystemSettings? GetCachedSystemSettings(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session) && session is LaserDeviceSession hwqSession
            ? hwqSession.CachedSystemSettings?.Clone()
            : null;
    }

    public LaserSystemSettings ReadSystemSettings(Guid deviceId)
    {
        return GetHwqSession(deviceId).ReadSystemSettings();
    }

    public LaserSystemSettings WriteSystemSettings(Guid deviceId, LaserSystemSettings settings)
    {
        return GetHwqSession(deviceId).WriteSystemSettings(settings);
    }

    public LaserRealtimeStatus? GetCachedRealtimeStatus(Guid deviceId)
    {
        return _sessions.TryGetValue(deviceId, out var session)
            ? session.CachedRealtimeStatus?.Clone()
            : null;
    }

    public LaserDisplaySnapshot? GetCachedDisplaySnapshot(Guid deviceId)
    {
        if (!_sessions.TryGetValue(deviceId, out var session) || !session.IsConnected)
        {
            return null;
        }

        var device = GetDevice(deviceId);
        var realtime = session.CachedRealtimeStatus ?? new LaserRealtimeStatus();
        if (session is HwdLaserDeviceSession hwdSession)
        {
            var wave = hwdSession.CachedWaveSettings ?? new HwdLaserWaveSettings();
            return new LaserDisplaySnapshot
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                Model = device.Model,
                RealtimePower = realtime.LaserPower,
                WaveNumber = wave.WaveNumber,
                WaveMode = wave.EmitMode.ToString().ToUpperInvariant(),
                TriggerMode = wave.TriggerMode == HwdTriggerMode.电平出光 ? "电平出光" : "脉冲出光",
                MaximumPower = wave.MaximumPower,
                PulseInterval = wave.PulseInterval,
                AveragePower = wave.AveragePower,
                SinglePointEnergy = wave.SinglePointEnergy,
                OutputPointCount = realtime.OutputPointCount,
                AnalogVoltage = realtime.AnalogVoltage
            };
        }

        var hwqSession = (LaserDeviceSession)session;
        var hwqWave = hwqSession.CachedWaveSettings ?? new LaserWaveSettings();
        var system = hwqSession.CachedSystemSettings ?? new LaserSystemSettings();
        return new LaserDisplaySnapshot
        {
            DeviceId = device.Id,
            DeviceName = device.Name,
            Model = device.Model,
            RealtimePower = realtime.LaserPower,
            WaveNumber = hwqWave.WaveNumber,
            WaveMode = hwqWave.WaveModeIndex == 1 ? "CW" : "QCW",
            TriggerMode = system.LaserTriggerModeIndex == 1 ? "电平出光" : "脉冲出光",
            MaximumPower = hwqWave.MaximumPeakPower,
            AveragePower = hwqWave.MaximumAveragePower,
            SinglePointEnergy = hwqWave.PresetLaserEnergy,
            OutputPointCount = realtime.OutputPointCount,
            AnalogVoltage = realtime.AnalogVoltage
        };
    }

    public LaserRealtimeStatus ReadRealtimeStatus(Guid deviceId)
    {
        return GetSession(deviceId).ReadRealtimeStatus();
    }

    public LaserRealtimeStatus ReadHwdFastRealtimeStatus(Guid deviceId)
    {
        return GetHwdSession(deviceId).ReadFastRealtimeStatus();
    }

    public void SetContinuousOutput(Guid deviceId, bool enabled)
    {
        GetSession(deviceId).SetContinuousOutput(enabled);
    }

    public void PointWeld(Guid deviceId)
    {
        GetSession(deviceId).PointWeld();
    }

    public void SetLaserLock(Guid deviceId, bool locked)
    {
        GetSession(deviceId).SetLaserLock(locked);
    }

    public void SetRedLight(Guid deviceId, bool enabled)
    {
        GetSession(deviceId).SetRedLight(enabled);
    }

    public void ClearPointCount(Guid deviceId)
    {
        GetSession(deviceId).ClearPointCount();
    }

    public void ClearError(Guid deviceId)
    {
        GetSession(deviceId).ClearError();
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
    }

    private ILaserDeviceSession GetSession(Guid deviceId)
    {
        if (_sessions.TryGetValue(deviceId, out var existing))
        {
            return existing;
        }

        var config = _devices.SingleOrDefault(device => device.Id == deviceId)
            ?? throw new InvalidOperationException("未找到指定激光器配置。");
        ILaserDeviceSession session = config.Model == LaserDeviceModel.HWD
            ? new HwdLaserDeviceSession(config, _serialPortFactory.Create())
            : new LaserDeviceSession(config, _serialPortFactory.Create());
        _sessions[deviceId] = session;
        return session;
    }

    private LaserDeviceSession GetHwqSession(Guid deviceId)
    {
        return GetSession(deviceId) as LaserDeviceSession
            ?? throw new InvalidOperationException("当前激光器不是 HWQ 型号。");
    }

    private HwdLaserDeviceSession GetHwdSession(Guid deviceId)
    {
        return GetSession(deviceId) as HwdLaserDeviceSession
            ?? throw new InvalidOperationException("当前激光器不是 HWD 型号。");
    }

    private LaserDeviceConfig GetDevice(Guid deviceId)
    {
        return _devices.SingleOrDefault(device => device.Id == deviceId)
            ?? throw new InvalidOperationException("未找到指定激光器配置。");
    }
}
