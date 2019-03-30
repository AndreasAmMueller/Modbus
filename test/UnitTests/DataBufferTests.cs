using AMWD.Modbus.Common.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UnitTests
{
	[TestClass]
	public class DataBufferTests
	{
		[TestMethod]
		public void ConstructorTest()
		{
			var db1 = new DataBuffer();
			Assert.AreEqual(0, db1.Length);
			Assert.AreEqual(0, db1.Buffer.Length);
			CollectionAssert.AreEqual(new byte[0], db1.Buffer);

			var db2 = new DataBuffer(12);
			Assert.AreEqual(12, db2.Length);
			Assert.AreEqual(12, db2.Buffer.Length);
			Assert.IsTrue(db2.Buffer.All(b => b == 0));
			CollectionAssert.AreEqual(new byte[12], db2.Buffer);

			var bytes = new byte[] { 1, 3, 2, 4, 6, 5, 7, 8, 9 };
			var db3 = new DataBuffer(bytes);
			Assert.AreEqual(9, db3.Length);
			Assert.AreEqual(9, db3.Buffer.Length);
			CollectionAssert.AreEqual(bytes, db3.Buffer);
			Assert.AreNotEqual(bytes, db3.Buffer);
			bytes[0] = 0;
			CollectionAssert.AreNotEqual(bytes, db3.Buffer);

			var db4 = new DataBuffer(db3);
			Assert.AreNotEqual(db3.Buffer, db4.Buffer);
			CollectionAssert.AreEqual(db3.Buffer, db4.Buffer);
		}
	}
}
