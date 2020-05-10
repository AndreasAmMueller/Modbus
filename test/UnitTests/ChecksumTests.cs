using System.Text;
using AMWD.Modbus.Common.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
	[TestClass]
	public class ChecksumTests
	{
		[TestMethod]
		public void Crc16Test()
		{
			byte[] bytes = Encoding.ASCII.GetBytes("0123456789");
			byte[] expected = new byte[] { 77, 67 };

			byte[] crc = bytes.CRC16();

			CollectionAssert.AreEqual(expected, crc);
		}
	}
}
