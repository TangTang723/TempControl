using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class HwdLaserDeviceSession : ILaserDeviceSession
{
    private readonly object _syncRoot = new();
    private readonly HwdLaserSettingModel _model;
    private int _fastRealtimePollCount;

    public HwdLaserDeviceSession(LaserDeviceConfig config, ILaserSerialPort serialPort)
        : this(config, new HwdLaserSettingModel(serialPort))
    {
    }

    public HwdLaserDeviceSession(LaserDeviceConfig config, HwdLaserSettingModel model)
    {
        Config = config.Clone();
        _model = model;
        ApplyAddress();
    }

    public LaserDeviceConfig Config { get; private set; }

    public bool IsConnected => _model.IsOpen();

    public HwdLaserWaveSettings? CachedWaveSettings { get; private set; }

    public HwdLaserSystemSettings? CachedSystemSettings { get; private set; }

    public LaserRealtimeStatus? CachedRealtimeStatus { get; private set; }

    public void UpdateConfig(LaserDeviceConfig config)
    {
        var connectionChanged = Config.PortName != config.PortName
            || Config.BaudRate != config.BaudRate
            || Config.DeviceAddress != config.DeviceAddress;
        if (connectionChanged && IsConnected)
        {
            Disconnect();
        }

        Config = config.Clone();
        ApplyAddress();
    }

    public void Connect()
    {
        lock (_syncRoot)
        {
            if (!IsConnected)
            {
                ApplyAddress();
                _model.Open(Config.PortName, Config.BaudRate);
            }
        }
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            _model.Close();
        }
    }

    public HwdLaserWaveSettings ReadWaveSettings()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            return ReadWaveNumberSettingsCore(1);
        }
    }

    public HwdLaserWaveSettings ReadWaveNumberSettings(int waveNumber)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            return ReadWaveNumberSettingsCore(waveNumber);
        }
    }

    public HwdLaserWaveSettings WriteWaveSettings(HwdLaserWaveSettings settings)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var payload = HwdLaserWaveCodec.Serialize(settings, Config.HwdPowerScale);
            if (!_model.SetWaveData(payload))
            {
                throw new InvalidOperationException("HWD 保存波形失败。");
            }

            return ReadWaveNumberSettingsCore(settings.WaveNumber);
        }
    }

    public HwdLaserWaveSettings SwitchWave(int waveNumber)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            if (!_model.SetWaveNumber((byte)waveNumber))
            {
                throw new InvalidOperationException("HWD 切换波形失败。");
            }

            CachedWaveSettings ??= new HwdLaserWaveSettings { WaveNumber = waveNumber };
            CachedWaveSettings.CurrentOutputWaveNumber = waveNumber;
            return CachedWaveSettings.Clone();
        }
    }

    public HwdLaserSystemSettings ReadSystemSettings()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            if (!_model.GetTriggerEnableStatus(out var internalTrigger)
                || !_model.GetWaveEnableStatus(out var internalWave))
            {
                throw new InvalidOperationException("HWD 系统参数读取失败。");
            }

            CachedSystemSettings = new HwdLaserSystemSettings
            {
                LaserTriggerExternalEnabled = !internalTrigger,
                WaveExternalEnabled = !internalWave
            };
            return CachedSystemSettings.Clone();
        }
    }

    public HwdLaserSystemSettings WriteSystemSettings(HwdLaserSystemSettings settings)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            if (!_model.SetInternalTrigger(!settings.LaserTriggerExternalEnabled)
                || !_model.SetWaveEnable(!settings.WaveExternalEnabled))
            {
                throw new InvalidOperationException("HWD 系统参数写入失败。");
            }

            CachedSystemSettings = settings.Clone();
            return CachedSystemSettings.Clone();
        }
    }

    public LaserRealtimeStatus ReadRealtimeStatus()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var status = CachedRealtimeStatus?.Clone() ?? new LaserRealtimeStatus();

            if (!_model.GetSoftwareLockStatus(out var locked)
                || !_model.GetRedLaserStatus(out var redLight)
                || !_model.GetAlarmStatus(out var alarmData)
                || !_model.GetLaserOutputStatus(out var outputStatus))
            {
                throw new InvalidOperationException("HWD 实时状态读取失败。");
            }

            status.SoftwareLockActive = locked;
            status.StateHighByte = redLight ? (byte)0x04 : (byte)0x00;
            status.MachineLightByte = outputStatus == 0x00 ? (byte)0x20 : (byte)0x00;
            status.ErrorLowByte = alarmData.Length > 0 ? alarmData[0] : (byte)0;
            status.ErrorHighByte = alarmData.Skip(1).Any(value => value != 0)
                ? (byte)1
                : (byte)0;
            status.MachineAlarmByte = alarmData.Any(value => value != 0) ? (byte)0x00 : (byte)0x01;

            CachedRealtimeStatus = status;
            return status.Clone();
        }
    }

    public LaserRealtimeStatus ReadFastRealtimeStatus()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            if (!_model.GetFastRealtimeData(out var pointCount, out var realtimePower))
            {
                throw new InvalidOperationException("HWD 快速实时数据读取失败。");
            }

            var status = CachedRealtimeStatus?.Clone() ?? new LaserRealtimeStatus();
            status.OutputPointCount = pointCount;
            status.LaserPower = realtimePower;

            if (_fastRealtimePollCount % 2 == 0)
            {
                if (!_model.GetLaserOutputStatus(out var outputStatus))
                {
                    throw new InvalidOperationException("HWD 出光状态读取失败。");
                }

                status.MachineLightByte = outputStatus == 0x00 ? (byte)0x20 : (byte)0x00;
            }

            _fastRealtimePollCount++;
            CachedRealtimeStatus = status;
            return status.Clone();
        }
    }

    public void SetContinuousOutput(bool enabled)
    {
        ExecuteCommand(() => _model.SetContinuousOutput(enabled), "连续出光");
    }

    public void PointWeld()
    {
        ExecuteCommand(_model.PointWeld, "点焊");
    }

    public void SetLaserLock(bool locked)
    {
        ExecuteCommand(() => _model.SetLaserLock(locked), "锁光");
    }

    public void SetRedLight(bool enabled)
    {
        ExecuteCommand(() => _model.SetRedLaser(enabled), "红光");
    }

    public void ClearPointCount()
    {
        ExecuteCommand(_model.ClearPointCount, "清除出光点数");
    }

    public void ClearError()
    {
        ExecuteCommand(_model.ClearError, "清除错误");
    }

    public void Dispose()
    {
        _model.Dispose();
    }

    private HwdLaserWaveSettings ReadWaveNumberSettingsCore(int waveNumber)
    {
        if (!_model.GetWaveData((byte)waveNumber, out var payload))
        {
            throw new InvalidOperationException($"HWD 读取波形 {waveNumber} 失败。");
        }

        CachedWaveSettings = HwdLaserWaveCodec.Parse(payload, Config.HwdPowerScale);
        return CachedWaveSettings.Clone();
    }

    private void ExecuteCommand(Func<bool> command, string operationName)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            if (!command())
            {
                throw new InvalidOperationException($"HWD {operationName}操作失败。");
            }
        }
    }

    private void ApplyAddress()
    {
        _model.Address = Config.DeviceAddress == 0 ? (byte)0x01 : Config.DeviceAddress;
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException($"激光器 {Config.Name} 尚未连接。");
        }
    }
}
