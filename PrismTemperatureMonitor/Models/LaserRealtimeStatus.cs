using Prism.Mvvm;

namespace PrismTemperatureMonitor.Models;

public sealed class LaserRealtimeStatus : BindableBase
{
    private byte _errorLowByte;
    private byte _errorHighByte;
    private byte _stateLowByte;
    private byte _stateHighByte;
    private short _laserPower;
    private int _outputPointCount;
    private byte _machineAlarmByte = 0xFF;
    private byte _machineLightByte;
    private byte _powerStateByte;
    private byte _db25InputIo;
    private byte _db25InputWave;
    private byte _db25OutputIo;
    private byte _analogInput;
    private bool _softwareLockActive;

    public byte ErrorLowByte
    {
        get => _errorLowByte;
        set => SetProperty(ref _errorLowByte, value);
    }

    public byte ErrorHighByte
    {
        get => _errorHighByte;
        set => SetProperty(ref _errorHighByte, value);
    }

    public byte StateLowByte
    {
        get => _stateLowByte;
        set => SetProperty(ref _stateLowByte, value);
    }

    public byte StateHighByte
    {
        get => _stateHighByte;
        set => SetProperty(ref _stateHighByte, value);
    }

    public short LaserPower
    {
        get => _laserPower;
        set => SetProperty(ref _laserPower, value);
    }

    public int OutputPointCount
    {
        get => _outputPointCount;
        set => SetProperty(ref _outputPointCount, value);
    }

    public byte MachineAlarmByte
    {
        get => _machineAlarmByte;
        set => SetProperty(ref _machineAlarmByte, value);
    }

    public byte MachineLightByte
    {
        get => _machineLightByte;
        set => SetProperty(ref _machineLightByte, value);
    }

    public byte PowerStateByte
    {
        get => _powerStateByte;
        set => SetProperty(ref _powerStateByte, value);
    }

    public byte Db25InputIo
    {
        get => _db25InputIo;
        set => SetProperty(ref _db25InputIo, value);
    }

    public byte Db25InputWave
    {
        get => _db25InputWave;
        set => SetProperty(ref _db25InputWave, value);
    }

    public byte Db25OutputIo
    {
        get => _db25OutputIo;
        set => SetProperty(ref _db25OutputIo, value);
    }

    public byte AnalogInput
    {
        get => _analogInput;
        set => SetProperty(ref _analogInput, value);
    }

    public bool SoftwareLockActive
    {
        get => _softwareLockActive;
        set => SetProperty(ref _softwareLockActive, value);
    }

    public bool IsAlarmActive => ErrorLowByte != 0 || ErrorHighByte != 0 || (MachineAlarmByte & 0x01) == 0;

    public bool IsLaserOutputActive => (MachineLightByte & 0x20) != 0 || (Db25OutputIo & 0x01) != 0;

    public bool IsRedLightActive => (StateHighByte & 0x04) != 0;

    public double AnalogVoltage => AnalogInput / 10.0;

    public LaserRealtimeStatus Clone()
    {
        return new LaserRealtimeStatus
        {
            ErrorLowByte = ErrorLowByte,
            ErrorHighByte = ErrorHighByte,
            StateLowByte = StateLowByte,
            StateHighByte = StateHighByte,
            LaserPower = LaserPower,
            OutputPointCount = OutputPointCount,
            MachineAlarmByte = MachineAlarmByte,
            MachineLightByte = MachineLightByte,
            PowerStateByte = PowerStateByte,
            Db25InputIo = Db25InputIo,
            Db25InputWave = Db25InputWave,
            Db25OutputIo = Db25OutputIo,
            AnalogInput = AnalogInput,
            SoftwareLockActive = SoftwareLockActive
        };
    }
}
