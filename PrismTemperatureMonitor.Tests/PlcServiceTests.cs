using PrismTemperatureMonitor.Models;
using PrismTemperatureMonitor.Services;

namespace PrismTemperatureMonitor.Tests;

public sealed class PlcServiceTests
{
    [Fact]
    public void ReadMethods_ReadValuesDirectlyFromClientByAddress()
    {
        var client = new RecordingPlcClient();
        var boolAddress = new PlcAddress(1, 0, PlcValueType.Bool);
        var intAddress = new PlcAddress(1, 2, PlcValueType.Int);
        var floatAddress = new PlcAddress(1, 4, PlcValueType.Float);
        client.Values[boolAddress] = true;
        client.Values[intAddress] = 123;
        client.Values[floatAddress] = 45.6f;
        var service = new PlcService(client);

        Assert.True(service.ReadBool(boolAddress));
        Assert.Equal(123, service.ReadInt(intAddress));
        Assert.Equal(45.6f, service.ReadFloat(floatAddress));
        Assert.Equal([boolAddress, intAddress, floatAddress], client.ReadAddresses);
    }

    [Fact]
    public void WriteMethods_WriteValuesDirectlyToClientByAddress()
    {
        var client = new RecordingPlcClient();
        var boolAddress = new PlcAddress(1, 0, PlcValueType.Bool);
        var intAddress = new PlcAddress(1, 2, PlcValueType.Int);
        var floatAddress = new PlcAddress(1, 4, PlcValueType.Float);
        var service = new PlcService(client);

        service.WriteBool(boolAddress, true);
        service.WriteInt(intAddress, 123);
        service.WriteFloat(floatAddress, 45.6f);

        Assert.True((bool)client.Values[boolAddress]);
        Assert.Equal((short)123, client.Values[intAddress]);
        Assert.Equal(45.6f, client.Values[floatAddress]);
        Assert.Equal([boolAddress, intAddress, floatAddress], client.WriteAddresses);
    }

    [Fact]
    public void Read_ConnectsAutomaticallyWhenClientIsDisconnected()
    {
        var client = new RecordingPlcClient();
        var address = new PlcAddress(1, 4, PlcValueType.Float);
        client.Values[address] = 66.6f;
        var service = new PlcService(client);

        var value = service.ReadFloat(address);

        Assert.Equal(66.6f, value);
        Assert.Equal(1, client.ConnectCount);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public void Read_RetriesOnceAfterFailure()
    {
        var client = new RecordingPlcClient { ThrowReadCount = 1 };
        var address = new PlcAddress(1, 4, PlcValueType.Float);
        client.Values[address] = 88.8f;
        var service = new PlcService(client);

        var value = service.ReadFloat(address);

        Assert.Equal(88.8f, value);
        Assert.Equal(2, client.ConnectCount);
        Assert.Equal(1, client.DisconnectCount);
        Assert.Equal(2, client.ReadAddresses.Count);
    }

    [Fact]
    public void Read_ThrowsWhenRetryAlsoFails()
    {
        var client = new RecordingPlcClient { ThrowReadCount = 2 };
        var service = new PlcService(client);

        Assert.Throws<InvalidOperationException>(() => service.ReadFloat(new PlcAddress(1, 4, PlcValueType.Float)));
        Assert.Equal(2, client.ConnectCount);
        Assert.Equal(1, client.DisconnectCount);
    }

    private sealed class RecordingPlcClient : IPlcClient
    {
        public Dictionary<PlcAddress, object> Values { get; } = [];

        public List<PlcAddress> ReadAddresses { get; } = [];

        public List<PlcAddress> WriteAddresses { get; } = [];

        public string IpAddress { get; set; } = "192.168.0.10";

        public bool IsConnected { get; private set; }

        public int ConnectCount { get; private set; }

        public int DisconnectCount { get; private set; }

        public int ThrowReadCount { get; set; }

        public void Connect()
        {
            ConnectCount++;
            IsConnected = true;
        }

        public void Disconnect()
        {
            DisconnectCount++;
            IsConnected = false;
        }

        public object Read(PlcAddress address)
        {
            ReadAddresses.Add(address);
            if (ThrowReadCount > 0)
            {
                ThrowReadCount--;
                throw new InvalidOperationException("Simulated PLC read failure.");
            }

            return Values.GetValueOrDefault(address, GetDefault(address.ValueType));
        }

        public void Write(PlcAddress address, object value)
        {
            WriteAddresses.Add(address);
            Values[address] = value;
        }

        private static object GetDefault(PlcValueType valueType)
        {
            return valueType switch
            {
                PlcValueType.Bool => false,
                PlcValueType.Int => 0,
                PlcValueType.Float => 0f,
                _ => throw new InvalidOperationException()
            };
        }
    }
}
