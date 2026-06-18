namespace PrismTemperatureMonitor.Services;

public sealed class HwdLaserSettingModel : IDisposable
{
    private const byte FrameBoundary = 0x7E;
    private const byte Escape = 0x7D;
    private const byte DefaultAddress = 0x01;
    private readonly object _syncRoot = new();
    private readonly ILaserSerialPort _serialPort;
    private readonly bool _ownsSerialPort;

    public HwdLaserSettingModel()
        : this(new SystemLaserSerialPort(), true)
    {
    }

    public HwdLaserSettingModel(ILaserSerialPort serialPort)
        : this(serialPort, false)
    {
    }

    private HwdLaserSettingModel(ILaserSerialPort serialPort, bool ownsSerialPort)
    {
        _serialPort = serialPort;
        _ownsSerialPort = ownsSerialPort;
        Address = DefaultAddress;
    }

    public byte Address { get; set; }

    public int CommandDelayMilliseconds { get; set; } = 100;

    public void Open(string portName, int baudRate = 115200)
    {
        if (_serialPort.IsOpen)
        {
            throw new InvalidOperationException("HWD laser serial port is already open.");
        }

        _serialPort.PortName = portName;
        _serialPort.BaudRate = baudRate;
        _serialPort.ReadTimeout = 500;
        _serialPort.Open();
    }

    public bool IsOpen()
    {
        return _serialPort.IsOpen;
    }

    public void Close()
    {
        _serialPort.Close();
    }

    public bool SetLaserLock(bool locked)
    {
        return SendBooleanCommand(0x10, locked);
    }

    public bool ClearPointCount()
    {
        return SendSimpleCommand(0x11, 0x01);
    }

    public bool SetContinuousOutput(bool enabled)
    {
        return SendBooleanCommand(0x12, enabled);
    }

    public bool PointWeld()
    {
        return SendSimpleCommand(0x13, 0x01);
    }

    public bool SetInternalTrigger(bool internalTrigger)
    {
        return SendBooleanCommand(0x14, internalTrigger);
    }

    public bool SetModuleEnable(byte moduleData1, byte moduleData2)
    {
        return SendCommand(0x15, [moduleData2, moduleData1]) is not null;
    }

    public bool SetRedLaser(bool enabled)
    {
        return SendBooleanCommand(0x16, enabled);
    }

    public bool SetWaveEnable(bool internalEnable)
    {
        return SendBooleanCommand(0x17, internalEnable);
    }

    public bool SetWaveNumber(byte waveNumber)
    {
        return SendSimpleCommand(0x18, waveNumber);
    }

    public bool SetWaveData(byte[] waveData)
    {
        return SendCommand(0x1A, waveData) is not null;
    }

    public bool ClearError()
    {
        return SendSimpleCommand(0x1C, 0x01);
    }

    public bool GetVersion(out byte[] versionData)
    {
        versionData = [];
        var payload = SendCommand(0x20, []);
        if (payload is null)
        {
            return false;
        }

        versionData = payload;
        return true;
    }

    public bool GetPointCount(out int count)
    {
        return GetFastRealtimeData(out count, out _);
    }

    public bool GetFastRealtimeData(out int count, out short realtimePower)
    {
        count = 0;
        realtimePower = 0;
        var payload = SendCommand(0x21, []);
        if (payload is null || payload.Length < 6)
        {
            return false;
        }

        count = unchecked((int)(
            ((uint)payload[3] << 24)
            | ((uint)payload[2] << 16)
            | ((uint)payload[1] << 8)
            | payload[0]));
        realtimePower = unchecked((short)((payload[5] << 8) | payload[4]));
        return true;
    }

    public bool GetMainControlParameters(out byte[] parameters)
    {
        return TryReadRaw(0x22, out parameters);
    }

    public bool GetWaveNumber(out byte waveNumber)
    {
        waveNumber = 0;
        var payload = SendCommand(0x23, []);
        if (payload is null || payload.Length == 0)
        {
            return false;
        }

        waveNumber = payload[0];
        return true;
    }

    public bool GetWaveData(byte waveNumber, out byte[] waveData)
    {
        waveData = [];
        var payload = SendCommand(0x24, [waveNumber]);
        if (payload is null)
        {
            return false;
        }

        waveData = payload;
        return true;
    }

    public bool GetSoftwareLockStatus(out bool locked)
    {
        return TryReadBoolean(0x25, out locked);
    }

    public bool GetHardwareLockStatus(out bool locked)
    {
        return TryReadBoolean(0x26, out locked);
    }

    public bool GetTriggerEnableStatus(out bool internalTrigger)
    {
        return TryReadBoolean(0x27, out internalTrigger);
    }

    public bool GetWaveEnableStatus(out bool internalEnable)
    {
        return TryReadBoolean(0x28, out internalEnable);
    }

    public bool GetRedLaserStatus(out bool enabled)
    {
        return TryReadBoolean(0x29, out enabled);
    }

    public bool GetModuleEnableStatus(out byte moduleData1, out byte moduleData2)
    {
        moduleData1 = 0;
        moduleData2 = 0;
        var payload = SendCommand(0x2A, []);
        if (payload is null || payload.Length < 2)
        {
            return false;
        }

        moduleData2 = payload[0];
        moduleData1 = payload[1];
        return true;
    }

    public bool GetAlarmStatus(out byte[] alarmData)
    {
        return TryReadRaw(0x2C, out alarmData);
    }

    public bool GetLaserOutputStatus(out byte status)
    {
        status = 0;
        var payload = SendCommand(0x2E, []);
        if (payload is null || payload.Length == 0)
        {
            return false;
        }

        status = payload[0];
        return true;
    }

    public bool GetIoStatus(out byte[] ioData)
    {
        return TryReadRaw(0x37, out ioData);
    }

    public bool GetAnalogValue(out byte[] analogData)
    {
        return TryReadRaw(0x3C, out analogData);
    }

    public byte[] AppendCheckBytes(byte[] original)
    {
        if (original.Length < 3 || original[0] != FrameBoundary)
        {
            throw new ArgumentException("HWD command frame must start with 0x7E.", nameof(original));
        }

        var sourceLength = original[^1] == FrameBoundary ? original.Length - 1 : original.Length;
        var frame = new byte[sourceLength + 3];
        Array.Copy(original, 0, frame, 0, sourceLength);
        frame[^1] = FrameBoundary;
        FillCheckBytes(frame);
        return frame;
    }

    public byte[] EncodeFrame(byte[] frame)
    {
        if (frame.Length < 2 || frame[0] != FrameBoundary || frame[^1] != FrameBoundary)
        {
            throw new ArgumentException("HWD command frame must start and end with 0x7E.", nameof(frame));
        }

        var encoded = new List<byte> { FrameBoundary };
        for (var index = 1; index < frame.Length - 1; index++)
        {
            if (frame[index] == FrameBoundary)
            {
                encoded.Add(Escape);
                encoded.Add(0x5E);
            }
            else if (frame[index] == Escape)
            {
                encoded.Add(Escape);
                encoded.Add(0x5D);
            }
            else
            {
                encoded.Add(frame[index]);
            }
        }

        encoded.Add(FrameBoundary);
        return encoded.ToArray();
    }

    public byte[] DecodeFrame(byte[] buffer, int count)
    {
        if (count < 2 || buffer[0] != FrameBoundary || buffer[count - 1] != FrameBoundary)
        {
            throw new ArgumentException("HWD response frame format is invalid.", nameof(buffer));
        }

        var decoded = new List<byte> { FrameBoundary };
        for (var index = 1; index < count - 1; index++)
        {
            if (buffer[index] == Escape)
            {
                if (index + 1 >= count - 1)
                {
                    throw new ArgumentException("HWD response frame escape format is invalid.", nameof(buffer));
                }

                if (buffer[index + 1] == 0x5E)
                {
                    decoded.Add(FrameBoundary);
                    index++;
                }
                else if (buffer[index + 1] == 0x5D)
                {
                    decoded.Add(Escape);
                    index++;
                }
            }
            else
            {
                decoded.Add(buffer[index]);
            }
        }

        decoded.Add(FrameBoundary);
        return decoded.ToArray();
    }

    public ushort CalcFcs(byte[] data)
    {
        ushort fcs = 0xFFFF;
        foreach (var item in data)
        {
            var j = (ushort)((fcs ^ item) & 15);
            fcs = (ushort)(fcs >> 4);
            fcs = (ushort)(fcs ^ (j * 4225));
            j = (ushort)((fcs ^ (item >> 4)) & 15);
            fcs = (ushort)(fcs >> 4);
            fcs = (ushort)(fcs ^ (j * 4225));
        }

        return (ushort)(fcs ^ 0xFFFF);
    }

    public void Dispose()
    {
        Close();
        if (_ownsSerialPort && _serialPort is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private bool SendBooleanCommand(byte command, bool enabled)
    {
        return SendSimpleCommand(command, enabled ? (byte)0x01 : (byte)0x00);
    }

    private bool SendSimpleCommand(byte command, byte data)
    {
        var payload = SendCommand(command, [data]);
        return payload is not null && payload.Length > 0 && payload[0] == data;
    }

    private bool TryReadBoolean(byte command, out bool value)
    {
        value = false;
        var payload = SendCommand(command, []);
        if (payload is null || payload.Length == 0)
        {
            return false;
        }

        value = payload[0] == 0x01;
        return true;
    }

    private bool TryReadRaw(byte command, out byte[] data)
    {
        data = [];
        var payload = SendCommand(command, []);
        if (payload is null)
        {
            return false;
        }

        data = payload;
        return true;
    }

    private byte[]? SendCommand(byte command, byte[] data, int timeout = 300)
    {
        lock (_syncRoot)
        {
            if (!_serialPort.IsOpen)
            {
                throw new InvalidOperationException("HWD laser serial port is not open.");
            }

            var raw = BuildCommandFrame(command, data);
            var receiveBuffer = new byte[1024];
            _serialPort.Write(raw, 0, raw.Length);
            if (command == 0x1A)
            {
                Thread.Sleep(400);
            }
            Thread.Sleep(CommandDelayMilliseconds >= 0 ? CommandDelayMilliseconds : timeout);
            var count = _serialPort.Read(receiveBuffer, 0, receiveBuffer.Length);
            return ExtractPayload(receiveBuffer, count, command);
        }
    }

    private byte[] BuildCommandFrame(byte command, byte[] data)
    {
        var raw = new byte[data.Length + 4];
        raw[0] = FrameBoundary;
        raw[1] = Address;
        raw[2] = command;
        Array.Copy(data, 0, raw, 3, data.Length);
        raw[^1] = FrameBoundary;
        return AppendCheckBytes(raw);
    }

    private byte[]? ExtractPayload(byte[] encodedResponse, int count, byte expectedCommand)
    {
        var decoded = DecodeFrame(encodedResponse, count);
        if (decoded.Length < 6 || decoded[1] != Address || decoded[2] != expectedCommand)
        {
            return null;
        }

        var payloadForCheck = decoded.Skip(1).Take(decoded.Length - 4).ToArray();
        var expectedFcs = CalcFcs(payloadForCheck);
        var actualFcs = BitConverter.ToUInt16([decoded[^3], decoded[^2]]);
        if (expectedFcs != actualFcs)
        {
            return null;
        }

        return decoded.Skip(3).Take(decoded.Length - 6).ToArray();
    }

    private void FillCheckBytes(byte[] frame)
    {
        var payload = frame.Skip(1).Take(frame.Length - 4).ToArray();
        var fcs = BitConverter.GetBytes(CalcFcs(payload));
        frame[^3] = fcs[0];
        frame[^2] = fcs[1];
    }
}
