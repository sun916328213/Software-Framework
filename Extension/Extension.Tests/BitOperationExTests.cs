using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Extension.Core.Comparison;
using Xunit;

namespace Extension.Tests;

public class BitOperationExTests
{
	[Fact]
	public void Compare_Ptr_null抛裸Exception而不是ArgumentException()
	{
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(null, new byte[1]));
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(new byte[1], null));
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(null, new ushort[1]));
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(null, new short[1]));
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(null, new bool[1]));
	}

	[Fact]
	public void Compare_Ptr_长度不等抛裸Exception()
	{
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(new byte[2], new byte[3]));
		Assert.Throws<Exception>(() => BitOperationEx.Compare_Ptr(new ushort[1], new ushort[2]));
	}

	[Fact]
	public void Compare_Ptr_两个空数组会抛NullReferenceException_是缺陷()
	{
		Assert.Throws<NullReferenceException>(() => BitOperationEx.Compare_Ptr(new byte[0], new byte[0]));
		Assert.Throws<NullReferenceException>(() => BitOperationEx.Compare_Ptr(new ushort[0], new ushort[0]));
		Assert.Empty(BitOperationEx.Compare_Ptr(new byte[1], new byte[1]));
		Assert.Empty(BitOperationEx.Compare_Ptr(new byte[1], new byte[1], EComparePolarity.BothEdges, ECompareMode.Sixteen));
	}

	[Fact]
	public void Compare_Ptr_默认模式是Eight_逐字节比对()
	{
		byte[] b = new byte[3] { 1, 2, 3 };
		byte[] b2 = new byte[3] { 1, 255, 3 };
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
		Assert.Equal((byte)2, (byte)list[0].Value_Current);
		Assert.Equal(byte.MaxValue, (byte)list[0].Value_Last);
	}

	[Fact]
	public void Compare_Ptr_默认极性是BothEdges_不等就记录()
	{
		byte[] b = new byte[4] { 1, 2, 3, 4 };
		byte[] b2 = new byte[4] { 1, 9, 3, 4 };
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
	}

	[Fact]
	public void Compare_Ptr_完全相同返回空()
	{
		byte[] array = new byte[3] { 1, 2, 3 };
		Assert.Empty(BitOperationEx.Compare_Ptr(array, (byte[])array.Clone()));
	}

	[Fact]
	public void Compare_Ptr_Eight模式_能报出多处差异且下标正确()
	{
		byte[] b = new byte[5] { 0, 1, 2, 3, 4 };
		byte[] b2 = new byte[5] { 16, 1, 32, 3, 64 };
		List<CompareValue> source = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Equal(new int[3] { 0, 2, 4 }, source.Select((CompareValue d) => d.Index).ToArray());
	}

	[Fact]
	public void Compare_Ptr_RisingEdge_只记当前值大于上次()
	{
		byte[] b = new byte[3] { 5, 1, 5 };
		byte[] b2 = new byte[3] { 1, 5, 5 };
		List<CompareValue> source = BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.RisingEdge);
		Assert.Equal(new int[1], source.Select((CompareValue d) => d.Index).ToArray());
		List<CompareValue> source2 = BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.FallingEdge);
		Assert.Equal(new int[1] { 1 }, source2.Select((CompareValue d) => d.Index).ToArray());
		List<CompareValue> source3 = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Equal(new int[2] { 0, 1 }, source3.Select((CompareValue d) => d.Index).ToArray());
	}

	[Fact]
	public void Compare_Ptr_None极性_什么都不记录()
	{
		byte[] b = new byte[2] { 255, 255 };
		byte[] b2 = new byte[2];
		Assert.Empty(BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.None));
	}

	[Fact]
	public void Compare_Ptr_相等时不记录任何极性()
	{
		byte[] b = new byte[2] { 7, 7 };
		byte[] b2 = new byte[2] { 7, 7 };
		EComparePolarity[] array = new EComparePolarity[4]
		{
			EComparePolarity.RisingEdge,
			EComparePolarity.FallingEdge,
			EComparePolarity.BothEdges,
			EComparePolarity.None
		};
		foreach (EComparePolarity pCompareMode in array)
		{
			Assert.Empty(BitOperationEx.Compare_Ptr(b, b2, pCompareMode));
		}
	}

	[Fact]
	public void Compare_Ptr_Sixteen模式_差异落在第2字节的高位时漏报()
	{
		byte[] b = new byte[2] { 0, 4 };
		byte[] b2 = new byte[2];
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
		List<CompareValue> collection = BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.BothEdges, ECompareMode.Sixteen);
		Assert.Empty(collection);
		byte[] b3 = new byte[2] { 4, 0 };
		byte[] b4 = new byte[2];
		List<CompareValue> list2 = BitOperationEx.Compare_Ptr(b3, b4, EComparePolarity.BothEdges, ECompareMode.Sixteen);
		Assert.Single(list2);
		Assert.Equal(0, list2[0].Index);
	}

	[Fact]
	public void Compare_Ptr_Thirty_Two模式_同样漏报()
	{
		byte[] b = new byte[4] { 0, 0, 4, 0 };
		byte[] b2 = new byte[4];
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(2, list[0].Index);
		List<CompareValue> collection = BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.BothEdges, ECompareMode.Thirty_Two);
		Assert.Empty(collection);
	}

	[Fact]
	public void Compare_Ptr_Eight与Sixty_Four模式不受该缺陷影响()
	{
		byte[] b = new byte[8] { 0, 0, 0, 0, 0, 0, 0, 4 };
		byte[] b2 = new byte[8];
		Assert.Single(BitOperationEx.Compare_Ptr(b, b2));
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.BothEdges, ECompareMode.Sixty_Four);
		Assert.Single(list);
		Assert.Equal(7, list[0].Index);
	}

	[Fact]
	public void Compare_Ptr_ushort数组()
	{
		ushort[] b = new ushort[3] { 1, 2, 3 };
		ushort[] b2 = new ushort[3] { 1, 9, 3 };
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
		Assert.Equal((ushort)2, (ushort)list[0].Value_Current);
		Assert.Equal((ushort)9, (ushort)list[0].Value_Last);
	}

	[Fact]
	public void Compare_Ptr_short数组_支持负数()
	{
		short[] b = new short[2] { -1, -2 };
		short[] b2 = new short[2] { -1, -2 };
		Assert.Empty(BitOperationEx.Compare_Ptr(b, b2));
		short[] b3 = new short[2] { -1, 2 };
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b3, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
		Assert.Single(BitOperationEx.Compare_Ptr(b3, b2, EComparePolarity.RisingEdge));
		Assert.Empty(BitOperationEx.Compare_Ptr(b3, b2, EComparePolarity.FallingEdge));
	}

	[Fact]
	public void Compare_Ptr_bool数组()
	{
		bool[] b = new bool[3] { false, true, false };
		bool[] b2 = new bool[3];
		List<CompareValue> list = BitOperationEx.Compare_Ptr(b, b2);
		Assert.Single(list);
		Assert.Equal(1, list[0].Index);
		Assert.True((bool)list[0].Value_Current);
		Assert.False((bool)list[0].Value_Last);
		Assert.Single(BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.RisingEdge));
		Assert.Empty(BitOperationEx.Compare_Ptr(b, b2, EComparePolarity.FallingEdge));
	}

	[Fact]
	public void ECompareMode的数值是2的幂指数()
	{
		Assert.Equal(0, 0);
		Assert.Equal(1, 1);
		Assert.Equal(2, 2);
		Assert.Equal(3, 3);
	}

	[Fact]
	public void EComparePolarity的取值顺序()
	{
		Assert.Equal(0, 0);
		Assert.Equal(1, 1);
		Assert.Equal(2, 2);
		Assert.Equal(3, 3);
	}

	[Fact]
	public void CompareValue是可变结构体_三个属性都可写()
	{
		CompareValue compareValue = new CompareValue
		{
			Index = 1,
			Value_Current = 2,
			Value_Last = 3
		};
		Assert.Equal(1, compareValue.Index);
		Assert.Equal(2, compareValue.Value_Current);
		Assert.Equal(3, compareValue.Value_Last);
		Assert.True(typeof(CompareValue).IsValueType);
	}

	[Fact]
	public void BitOperationEx是静态类()
	{
		Assert.True(typeof(BitOperationEx).IsAbstract && typeof(BitOperationEx).IsSealed);
	}

	[Fact]
	public void BitOperationEx_泛型指针方法必须带unmanaged约束()
	{
		string text = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Core", "Comparison", "BitOperationEx.cs"));
		Assert.Contains("ECompareMode pCompareSpeed = ECompareMode.Sixteen) where T : unmanaged", text);
		Assert.Contains("EComparePolarity pCompareMode) where T : unmanaged", text);
		int actual = text.Split('\n').Count((string l) => l.Contains("where T : unmanaged") && !l.TrimStart().StartsWith("///"));
		Assert.Equal(2, actual);
		Assert.Contains("static void ArrayCompare<T>(", text);
		Assert.Contains("static void Handle<T>(", text);
	}

	private static string ExtensionSourceRoot([CallerFilePath] string thisFile = "")
	{
		string directoryName = Path.GetDirectoryName(thisFile);
		Assert.False(string.IsNullOrEmpty(directoryName), "无法从 CallerFilePath 解析出测试目录");
		return Path.GetFullPath(Path.Combine(directoryName, "..", "Extension"));
	}
}
