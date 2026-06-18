using PrismTemperatureMonitor.Models;

namespace PrismTemperatureMonitor.Services;

public interface IPlcService
{
    string IpAddress { get; set; }

    bool IsConnected { get; }

    void Connect();

    void Disconnect();

     bool ReadBool(PlcAddress address);

    int ReadInt(PlcAddress address);

    float ReadFloat(PlcAddress address);

    void WriteBool(PlcAddress address, bool value);

    void WriteInt(PlcAddress address, int value);
    void WriteDInt(PlcAddress address, int value);

    void WriteFloat(PlcAddress address, float value);
}
