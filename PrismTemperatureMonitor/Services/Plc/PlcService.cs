using System.Net;
using PrismTemperatureMonitor.Models;
using S7.Net;

namespace PrismTemperatureMonitor.Services;

public sealed class PlcService : IPlcService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IPlcClient _client;

    public PlcService(IPlcClient client)
    {
        _client = client;
    }

    private PlcService()
        : this(new S7PlcClient("192.168.0.10"))
    {
    }

    public static PlcService Instance { get; } = new();

    public string IpAddress
    {
        get => _client.IpAddress;
        set => _client.IpAddress = value;
    }

    public bool IsConnected => _client.IsConnected;

    public void Configure(string ipAddress, CpuType cpuType = CpuType.S71500, short rack = 0, short slot = 0)
    {
        IpAddress = ipAddress;
        if (_client is S7PlcClient s7Client)
        {
            s7Client.Configure(cpuType, rack, slot);
        }
    }

    public void Connect()
    {
        lock (_syncRoot)
        {
            _client.Connect();
        }
    }

    public void Disconnect()
    {
        lock (_syncRoot)
        {
            _client.Disconnect();
        }
    }

    public bool ReadBool(PlcAddress address)
    {
        return ExecuteWithReconnect(() => Convert.ToBoolean(_client.Read(address)));
    }

    public int ReadInt(PlcAddress address)
    {
        return ExecuteWithReconnect(() => Convert.ToInt32(_client.Read(address)));
    }

    public float ReadFloat(PlcAddress address)
    {
        //return ExecuteWithReconnect(() => Convert.ToSingle(_client.Read(address)));
        return ExecuteWithReconnect(() => ((uint)_client.Read(address)).ConvertToFloat());
       
    }

    public void WriteBool(PlcAddress address, bool value)
    {
        ExecuteWithReconnect(() => _client.Write(address, value));
    }

    public void WriteInt(PlcAddress address, int value)
    {
        ExecuteWithReconnect(() => _client.Write(address, (short)value));
    }
    public void WriteDInt(PlcAddress address, int value)
    {
        ExecuteWithReconnect(() => _client.Write(address, value));
    }
    public void WriteFloat(PlcAddress address, float value)
    {
        ExecuteWithReconnect(() => _client.Write(address, value));
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _client.Disconnect();
        }
    }

    private void ExecuteWithReconnect(Action operation)
    {
        lock (_syncRoot)
        {
            EnsureConnectedCore();
            try
            {
                operation();
            }
            catch
            {
                _client.Disconnect();
                EnsureConnectedCore();
                operation();
            }
        }
    }

    private T ExecuteWithReconnect<T>(Func<T> operation)
    {
        lock (_syncRoot)
        {
            EnsureConnectedCore();
            try
            {
                return operation();
            }
            catch
            {
                _client.Disconnect();
                EnsureConnectedCore();
                return operation();
            }
        }
    }

    private void EnsureConnectedCore()
    {
        if (_client.IsConnected)
        {
            return;
        }

        _client.Connect();
    }
}
