using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public sealed class LaserDeviceSession : ILaserDeviceSession
{
    private readonly object _syncRoot = new();
    private readonly ILaserSerialPort _serialPort;

    public LaserDeviceSession(LaserDeviceConfig config, ILaserSerialPort serialPort)
    {
        Config = config.Clone();
        _serialPort = serialPort;
    }

    public LaserDeviceConfig Config { get; private set; }

    public bool IsConnected => _serialPort.IsOpen;

    public LaserWaveSettings? CachedWaveSettings { get; private set; }

    public LaserSystemSettings? CachedSystemSettings { get; private set; }

    public LaserRealtimeStatus? CachedRealtimeStatus { get; private set; }

    public void UpdateConfig(LaserDeviceConfig config)
    {
        Config = config.Clone();
    }

    public void Connect()
    {
        lock (_syncRoot)
        {
            if (_serialPort.IsOpen)
            {
                return;
            }

            _serialPort.PortName = Config.PortName;
            _serialPort.BaudRate = Config.BaudRate;
            _serialPort.ReadTimeout = 500;
            _serialPort.Open();
        }
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            _serialPort.Close();
        }
    }

    public LaserWaveSettings ReadWaveSettings()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var waveNoPayload = SendCommand(LaserProtocol.BuildGetWaveNoCommand(), 0x03);
            var waveNumber = waveNoPayload.Length > 2 ? waveNoPayload[2] : 1;

            var wavePayload = SendCommand(LaserProtocol.BuildGetWaveParamCommand(waveNumber), 0x04);
            var waveBytes = NormalizeWaveParameterPayload(wavePayload, waveNumber);
            CachedWaveSettings = LaserProtocol.ParseWaveSettings(waveBytes);
            return CachedWaveSettings.Clone();
        }
    }
    public LaserWaveSettings ReadWaveNumberSettings(int Number)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var wavePayload = SendCommand(LaserProtocol.BuildGetWaveParamCommand(Number), 0x04);
            var waveBytes = NormalizeWaveParameterPayload(wavePayload, Number);
            CachedWaveSettings = LaserProtocol.ParseWaveSettings(waveBytes);
            return CachedWaveSettings.Clone();
        }
    }
    public LaserWaveSettings WriteWaveSettings(LaserWaveSettings settings)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var waveNumber = settings.WaveNumber;
            SendCommand(LaserProtocol.BuildSetWaveNoCommand(waveNumber), 0x83);
            var waveBytes = LaserProtocol.ToWaveParameterBytes(settings);
            var writePayload = SendCommand(LaserProtocol.BuildSetWaveParamCommand(waveBytes), 0x84);
            var confirmedBytes = NormalizeWaveParameterPayload(writePayload, waveNumber);
            CachedWaveSettings = LaserProtocol.ParseWaveSettings(confirmedBytes);
            return CachedWaveSettings.Clone();
        }
    }

    public LaserWaveSettings SwitchWave(int waveNumber)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            SendCommand(LaserProtocol.BuildSetWaveNoCommand(waveNumber), 0x83);
            return ReadWaveSettings();
        }
    }

    public LaserSystemSettings ReadSystemSettings()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var payload = SendCommand(LaserProtocol.BuildGetSystemSettingsCommand(), 0x02);
            CachedSystemSettings = LaserProtocol.ParseSystemSettings(payload);
            return CachedSystemSettings.Clone();
        }
    }

    public LaserSystemSettings WriteSystemSettings(LaserSystemSettings settings)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var payload = SendCommand(LaserProtocol.BuildSetSystemSettingsCommand(settings), 0x82);
            CachedSystemSettings = LaserProtocol.ParseSystemSettings(payload);
            return CachedSystemSettings.Clone();
        }
    }

    public LaserRealtimeStatus ReadRealtimeStatus()
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            var status = CachedRealtimeStatus?.Clone() ?? new LaserRealtimeStatus();

            var pointCountPayload = SendCommand(LaserProtocol.BuildGetPointCountCommand(), 0x01);
            status.OutputPointCount = LaserProtocol.ParsePointCount(pointCountPayload);

            var machineStatePayload = SendCommand(LaserProtocol.BuildGetMachineStateCommand(), 0x10);
            LaserProtocol.ApplyMachineState(status, machineStatePayload);

            var db25Payload = SendCommand(LaserProtocol.BuildGetDb25InfoCommand(), 0x12);
            LaserProtocol.ApplyDb25Info(status, db25Payload);

            var realtimePayload = SendCommand(LaserProtocol.BuildGetRealtimeParameterCommand(), 0x34);
            LaserProtocol.ApplyRealtimeParameter(status, realtimePayload);

            CachedRealtimeStatus = status;
            return status.Clone();
        }
    }

    public void SetContinuousOutput(bool enabled)
    {
        SendSimpleCommand(LaserProtocol.BuildContinuousOutputCommand(enabled), 0x94);
    }

    public void PointWeld()
    {
        SendSimpleCommand(LaserProtocol.BuildPointWeldCommand(), 0x93);
    }

    public void SetLaserLock(bool locked)
    {
        SendSimpleCommand(LaserProtocol.BuildLaserLockCommand(locked), 0x96);
    }

    public void SetRedLight(bool enabled)
    {
        SendSimpleCommand(LaserProtocol.BuildRedLightCommand(enabled), 0x92);
    }

    public void ClearPointCount()
    {
        SendSimpleCommand(LaserProtocol.BuildClearPointCountCommand(), 0x91);
    }

    public void ClearError()
    {
        SendSimpleCommand(LaserProtocol.BuildClearErrorCommand(), 0x90);
    }

    public void Dispose()
    {
        Disconnect();
        if (_serialPort is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private byte[] SendCommand(byte[] rawCommand, byte expectedCommand)
    {
        var encoded = LaserProtocol.EncodeFrame(rawCommand);
        var receiveBuffer = new byte[1024];
        _serialPort.Write(encoded, 0, encoded.Length);
        Thread.Sleep(50);
        var count = _serialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
        return LaserProtocol.ExtractPayload(receiveBuffer, count, expectedCommand);
    }

    private void SendSimpleCommand(byte[] rawCommand, byte expectedCommand)
    {
        lock (_syncRoot)
        {
            EnsureConnected();
            SendCommand(rawCommand, expectedCommand);
        }
    }

    private void EnsureConnected()
    {
        if (!_serialPort.IsOpen)
        {
            throw new InvalidOperationException($"激光器 {Config.Name} 尚未连接。");
        }
    }

    private static byte[] NormalizeWaveParameterPayload(byte[] payload, int waveNumber)
    {
        if (payload.Length >= 2 && payload[0] == 0x0F)
        {
            var data = payload.Skip(2).ToArray();
            if (data.Length >= 86)
            {
                return [payload[0], payload[1], .. data.Take(86)];
            }

            var normalized = new byte[86];
            var waveNumberBytes = BitConverter.GetBytes((short)waveNumber);
            normalized[0] = waveNumberBytes[0];
            normalized[1] = waveNumberBytes[1];
            Array.Copy(data, 0, normalized, 2, Math.Min(data.Length, 84));
            return [payload[0], payload[1], .. normalized];
        }

        if (payload.Length >= 88)
        {
            return payload.Take(88).ToArray();
        }

        if (payload.Length >= 86)
        {
            return [0x0F, 0x04, .. payload.Take(86)];
        }

        var bytes = new byte[86];
        bytes[0] = (byte)waveNumber;
        if (payload.Length > 3)
        {
            Array.Copy(payload, 3, bytes, 2, Math.Min(payload.Length - 3, 84));
        }

        return [0x0F, 0x04, .. bytes];
    }
}
