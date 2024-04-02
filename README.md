> **Impotant Information!**    
> As this implementation has way too many pitfalls, I decided to re-write the whole library.    
> You can find the new implementation here: https://github.com/AM-WD/AMWD.Protocols.Modbus

# Modbus

Implements the Modbus communication protocol, written as a .NET Standard 2.0 library.



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

For the people who have seen some devices: yes, it's a request to a Janitza device ;-).

## License

All packages published under the [MIT license](LICENSE.txt).
