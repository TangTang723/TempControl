using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface IPlcClient
{
    string IpAddress { get; set; }

    bool IsConnected { get; }

    void Connect();

    void Disconnect();

    object Read(PlcAddress address);

    void Write(PlcAddress address, object value);
}
