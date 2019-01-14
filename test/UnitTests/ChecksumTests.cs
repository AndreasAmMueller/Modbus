using System;
using AMWD.Modbus.Common.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace UnitTests
{
	[TestClass]
	public class ChecksumTests
	{
		[TestMethod]
		public void Crc16Test()
		{
			var bytes = Encoding.ASCII.GetBytes("0123456789");
			var expected = new byte[] { 77, 67 };

			var crc = bytes.CRC16();

			CollectionAssert.AreEqual(expected, crc);
		}
	}
}
