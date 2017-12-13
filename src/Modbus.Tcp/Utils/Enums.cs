using System;

namespace Modbus.Tcp.Utils
{
	internal enum MessageType
	{
		Unset,
		Read,
		WriteSingle,
		WriteMultiple
	}
}
