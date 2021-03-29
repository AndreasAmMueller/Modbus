using System.Text;
using AMWD.Modbus.Common.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
	[TestClass]
	public class ExtensionsTests
	{
		[TestMethod]
		public void UInt8Test()
		{
			byte expected = 170;
			var register = expected.ToModbusRegister(1);
			byte actual = register.GetByte();

			Assert.AreEqual(expected, actual, "Byte-conversion to register and back failed");
		}

		[TestMethod]
		public void UInt16Test()
		{
			ushort expected = 1234;
			var register = expected.ToModbusRegister(1);
			ushort actual = register.GetUInt16();

			Assert.AreEqual(expected, actual, "UInt16-conversion to register and back failed");
		}

		[TestMethod]
		public void UInt32Test()
		{
			uint expected = 703010;
			var registers = expected.ToModbusRegister(1);
			uint actual = registers.GetUInt32();
			
			Assert.AreEqual(expected, actual, "UInt32-conversion to register and back failed");
			Assert.AreEqual(1, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(2, registers[1].Address, "Addressing second register failed");
		}

		[TestMethod]
		public void UInt64Test()
		{
			ulong expected = ulong.MaxValue - byte.MaxValue;
			var registers = expected.ToModbusRegister(1);
			ulong actual = registers.GetUInt64();

			Assert.AreEqual(expected, actual, "UInt64-conversion to register and back failed");
			Assert.AreEqual(1, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(2, registers[1].Address, "Addressing second register failed");
			Assert.AreEqual(3, registers[2].Address, "Addressing third register failed");
			Assert.AreEqual(4, registers[3].Address, "Addressing fourth register failed");
		}

		[TestMethod]
		public void Int8Test()
		{
			sbyte expected = -85;
			var register = expected.ToModbusRegister(100);
			sbyte actual = register.GetSByte();

			Assert.AreEqual(expected, actual, "SByte-conversion to register and back failed");
		}

		[TestMethod]
		public void Int16Test()
		{
			short expected = -4321;
			var register = expected.ToModbusRegister(100);
			short actual = register.GetInt16();

			Assert.AreEqual(expected, actual, "Int16-conversion to register and back failed");
		}

		[TestMethod]
		public void Int32Test()
		{
			int expected = -4020;
			var registers = expected.ToModbusRegister(100);
			int actual = registers.GetInt32();

			Assert.AreEqual(expected, actual, "Int32-conversion to register and back failed");
			Assert.AreEqual(100, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(101, registers[1].Address, "Addressing second register failed");
		}

		[TestMethod]
		public void Int64Test()
		{
			long expected = long.MinValue + short.MaxValue;
			var registers = expected.ToModbusRegister(100);
			long actual = registers.GetInt64();

			Assert.AreEqual(expected, actual, "Int64-conversion to register and back failed");
			Assert.AreEqual(100, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(101, registers[1].Address, "Addressing second register failed");
			Assert.AreEqual(102, registers[2].Address, "Addressing third register failed");
			Assert.AreEqual(103, registers[3].Address, "Addressing fourth register failed");
		}

		[TestMethod]
		public void SingleTest()
		{
			float expected = 1.4263f;
			var registers = expected.ToModbusRegister(50);
			float actual = registers.GetSingle();

			Assert.AreEqual(expected, actual, "Single-conversion to register and back failed");
			Assert.AreEqual(50, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(51, registers[1].Address, "Addressing second register failed");
		}

		[TestMethod]
		public void DoubleTest()
		{
			double expected = double.MinValue + byte.MaxValue;
			var registers = expected.ToModbusRegister(50);
			double actual = registers.GetDouble();

			Assert.AreEqual(expected, actual, "Double-conversion to register and back failed");
			Assert.AreEqual(50, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(51, registers[1].Address, "Addressing second register failed");
			Assert.AreEqual(52, registers[2].Address, "Addressing third register failed");
			Assert.AreEqual(53, registers[3].Address, "Addressing fourth register failed");
		}

		[TestMethod]
		public void StringTest()
		{
			string expected = "0123456789123";
			var registers = expected.ToModbusRegister(42);
			string actual = registers.GetString(7);

			Assert.AreEqual(expected, actual, "String-conversion to register and back failed");
			Assert.AreEqual(42, registers[0].Address, "Addressing first register failed");
			Assert.AreEqual(43, registers[1].Address, "Addressing second register failed");
			Assert.AreEqual(44, registers[2].Address, "Addressing third register failed");
			Assert.AreEqual(45, registers[3].Address, "Addressing fourth register failed");
			Assert.AreEqual(46, registers[4].Address, "Addressing fifth register failed");
			Assert.AreEqual(47, registers[5].Address, "Addressing sixth register failed");
			Assert.AreEqual(48, registers[6].Address, "Addressing seventh register failed");
		}
	}
}
