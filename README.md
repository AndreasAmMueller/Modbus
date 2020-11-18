# Modbus

Implements the Modbus communication protocol, written as a .NET Standard 2.0 library.

| Package         | NuGet                                                                                                                                                    |
|-----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
| Modbus.Common   | [![NuGet](https://img.shields.io/nuget/v/AMWD.Modbus.Common.svg?style=flat-square)](https://www.nuget.org/packages/AMWD.Modbus.Common)                   |
| Modbus.Tcp      | [![NuGet](https://img.shields.io/nuget/v/AMWD.Modbus.Tcp.svg?style=flat-square)](https://www.nuget.org/packages/AMWD.Modbus.Tcp)                         |
| Modbus.Serial   | [![NuGet](https://img.shields.io/nuget/v/AMWD.Modbus.Serial.svg?style=flat-square)](https://www.nuget.org/packages/AMWD.Modbus.Serial)                   |
| Modbus.Proxy    | [![NuGet](https://img.shields.io/nuget/v/AMWD.Modbus.Proxy.svg?style=flat-square)](https://www.nuget.org/packages/AMWD.Modbus.Proxy)                     |
| Build Artifacts | [![pipeline status](https://git.am-wd.de/AM.WD/Modbus/badges/master/pipeline.svg?style=flat-square)](https://git.am-wd.de/AM.WD/Modbus/-/commits/master) |



## Example

You can use the clients without any big knowledge about the protocol:
```cs
string host = "modbus-device.local";
int port = 502;

using var client = new ModbusClient(host, port);
await client.Connect();

byte deviceIdentifier = 5;
ushort startAddress = 19000;
ushort count = 2;

var registers = await client.ReadHoldingRegisters(deviceIdentifier, startAddress, count);
float voltage = registers.GetSingle();

Console.WriteLine($"The voltage between L1 and N is: {voltage:N2}V");
```

For the people who have seen some devices; yes, it is a request to a Janitza device ;-).

## License

All packages published under the [MIT license](LICENSE.txt).
