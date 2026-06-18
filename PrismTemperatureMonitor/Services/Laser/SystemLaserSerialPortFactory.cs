namespace PrismTemperatureMonitor.Services;

public sealed class SystemLaserSerialPortFactory : ILaserSerialPortFactory
{
    public ILaserSerialPort Create()
    {
        return new SystemLaserSerialPort();
    }
}
