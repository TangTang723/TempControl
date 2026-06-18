using PrismTemperatureMonitor.Models;
using S7.Net;

namespace PrismTemperatureMonitor.Services;

public sealed class S7PlcClient : IPlcClient
{
    private Plc? _plc;
    private CpuType _cpuType;
    private short _rack;
    private short _slot;
    private string _ipAddress;

    public S7PlcClient(string ipAddress, CpuType cpuType = CpuType.S71500, short rack = 0, short slot = 0)
    {
        _ipAddress = ipAddress;
        _cpuType = cpuType;
        _rack = rack;
        _slot = slot;
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || _ipAddress == value.Trim())
            {
                return;
            }

            _ipAddress = value.Trim();
            Disconnect();
        }
    }

    public bool IsConnected => _plc?.IsConnected ?? false;

    public void Configure(CpuType cpuType = CpuType.S71500, short rack = 0, short slot = 0)
    {
        _cpuType = cpuType;
        _rack = rack;
        _slot = slot;
        Disconnect();
    }

    public void Connect()
    {
        if (_plc is { IsConnected: true })
        {
            return;
        }

        Disconnect();
        _plc = new Plc(_cpuType, _ipAddress, _rack, _slot);
        _plc.Open();
    }

    public void Disconnect()
    {
        try
        {
            _plc?.Close();
        }
        finally
        {
            _plc = null;
        }
    }

    public object Read(PlcAddress address)
    {
        var plc = _plc ?? throw new InvalidOperationException("PLC 尚未连接。");
        return plc.Read(address.ToS7Address())
            ?? throw new InvalidOperationException($"PLC 地址 {address.ToS7Address()} 读取结果为空。");
    }

    public void Write(PlcAddress address, object value)
    {
        var plc = _plc ?? throw new InvalidOperationException("PLC 尚未连接。");
        plc.Write(address.ToS7Address(), value);
    }
}
