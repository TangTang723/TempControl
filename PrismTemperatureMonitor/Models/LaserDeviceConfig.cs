namespace PrismTemperatureMonitor.Models;

public sealed class LaserDeviceConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "激光器";

    public string PortName { get; set; } = "COM1";

    public int BaudRate { get; set; } = 115200;

    public bool IsEnabled { get; set; } = true;

    public LaserDeviceModel Model { get; set; } = LaserDeviceModel.HWQ;

    public byte DeviceAddress { get; set; } = 0x01;

    public HwdPowerScale HwdPowerScale { get; set; } = HwdPowerScale.OneW;

    public LaserDeviceConfig Clone()
    {
        return new LaserDeviceConfig
        {
            Id = Id,
            Name = Name,
            PortName = PortName,
            BaudRate = BaudRate,
            IsEnabled = IsEnabled,
            Model = Model,
            DeviceAddress = DeviceAddress,
            HwdPowerScale = HwdPowerScale
        };
    }
}
