namespace PrismTemperatureMonitor.Services;

public interface ILaserSerialPort
{
    string PortName { get; set; }

    int BaudRate { get; set; }

    int ReadTimeout { get; set; }

    bool IsOpen { get; }

    void Open();

    void Close();

    void Write(byte[] buffer, int offset, int count);

    int Read(byte[] buffer, int offset, int count);
}
