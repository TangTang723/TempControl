using System.IO.Ports;

namespace PrismTemperatureMonitor.Services;

public sealed class SystemLaserSerialPort : ILaserSerialPort, IDisposable
{
    private readonly SerialPort _serialPort = new()
    {
        DataBits = 8,
        Parity = Parity.None,
        StopBits = StopBits.One,
        Handshake = Handshake.None
    };

    public string PortName
    {
        get => _serialPort.PortName;
        set => _serialPort.PortName = value;
    }

    public int BaudRate
    {
        get => _serialPort.BaudRate;
        set => _serialPort.BaudRate = value;
    }

    public int ReadTimeout
    {
        get => _serialPort.ReadTimeout;
        set => _serialPort.ReadTimeout = value;
    }

    public bool IsOpen => _serialPort.IsOpen;

    public void Open()
    {
        _serialPort.Open();
    }

    public void Close()
    {
        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        _serialPort.Write(buffer, offset, count);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _serialPort.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        _serialPort.Dispose();
    }
}
