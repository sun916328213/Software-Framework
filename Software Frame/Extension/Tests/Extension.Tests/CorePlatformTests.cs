// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CorePlatformTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Diagnostics、System.IO、System.Reflection、System.Runtime.CompilerServices、System.Runtime.InteropServices。导入不代表所有类型都会在运行时加载。
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Extension.Platform.Diagnostics;
using Extension.Platform.Memory;
using Extension.Platform.Native;
using Extension.Platform.Processes;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// CorePlatformTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class CorePlatformTests
{
	/// <summary>
	/// 验证 FpsEx_初始状态_帧率与总帧数都是0 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_初始状态_帧率与总帧数都是0()
	{
		FpsEx fpsEx = new FpsEx();
		Assert.Equal(0.0, fpsEx.CurrentFps);
		Assert.Equal(0, fpsEx.TotalFrameCount);
	}

	/// <summary>
	/// 验证 FpsEx_未达计算间隔_不更新帧率但总帧数照加 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Tick、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_未达计算间隔_不更新帧率但总帧数照加()
	{
		FpsEx fpsEx = new FpsEx();
		for (int i = 0; i < 9; i++)
		{
			fpsEx.Tick();
		}
		Assert.Equal(9, fpsEx.TotalFrameCount);
		Assert.Equal(0.0, fpsEx.CurrentFps);
	}

	/// <summary>
	/// 验证 FpsEx_达到计算间隔_算出帧率 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Tick、Sleep、Equal、True；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_达到计算间隔_算出帧率()
	{
		FpsEx fpsEx = new FpsEx(5);
		for (int i = 0; i < 5; i++)
		{
			fpsEx.Tick();
			Thread.Sleep(3);
		}
		Assert.Equal(5, fpsEx.TotalFrameCount);
		Assert.True(fpsEx.CurrentFps > 0.0, $"5 帧约 15ms，帧率应 > 0，实际 {fpsEx.CurrentFps}");
	}

	/// <summary>
	/// 验证 FpsEx_间隔参数非正数_回落到10 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetField、Equal、GetValue；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_间隔参数非正数_回落到10()
	{
		FieldInfo field = typeof(FpsEx).GetField("_calculationInterval", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.Equal(10, (int)field.GetValue(new FpsEx(0)));
		Assert.Equal(10, (int)field.GetValue(new FpsEx(-1)));
		Assert.Equal(3, (int)field.GetValue(new FpsEx(3)));
	}

	/// <summary>
	/// 验证 FpsEx_Stop之后Tick到间隔_会把停掉的秒表重启 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetValue、GetField、Tick、Stop、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_Stop之后Tick到间隔_会把停掉的秒表重启()
	{
		FpsEx fpsEx = new FpsEx(2);
		Stopwatch stopwatch = (Stopwatch)typeof(FpsEx).GetField("_stopwatch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fpsEx);
		fpsEx.Tick();
		fpsEx.Tick();
		fpsEx.Stop();
		Assert.False(stopwatch.IsRunning, "Stop() 后秒表应停止");
		fpsEx.Tick();
		fpsEx.Tick();
		Assert.True(stopwatch.IsRunning, "Tick 到间隔时 Restart() 会重启已 Stop 的秒表（缺陷）");
	}

	/// <summary>
	/// 验证 FpsEx_Reset_清空计数与帧率 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Tick、Reset、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_Reset_清空计数与帧率()
	{
		FpsEx fpsEx = new FpsEx(2);
		fpsEx.Tick();
		fpsEx.Tick();
		fpsEx.Reset();
		Assert.Equal(0, fpsEx.TotalFrameCount);
		Assert.Equal(0.0, fpsEx.CurrentFps);
	}

	/// <summary>
	/// 验证 FpsEx_格式化输出 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Tick、StartsWith、GetFormattedFps、Contains、GetDetailedFpsInfo；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void FpsEx_格式化输出()
	{
		FpsEx fpsEx = new FpsEx(2);
		fpsEx.Tick();
		fpsEx.Tick();
		Assert.StartsWith("FPS: ", fpsEx.GetFormattedFps());
		Assert.Contains("(Total: 2)", fpsEx.GetDetailedFpsInfo());
	}

	/// <summary>
	/// 验证 SafeMemoryHandle_把字节复制进非托管内存_可读回 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 CreateFromBytes、Equal、GetPointer、NotEqual、ReadByte；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void SafeMemoryHandle_把字节复制进非托管内存_可读回()
	{
		using SafeMemoryHandle safeMemoryHandle = SafeMemoryHandle.CreateFromBytes(new byte[3] { 1, 2, 3 });
		Assert.Equal(3, safeMemoryHandle.Size);
		nint pointer = safeMemoryHandle.GetPointer();
		Assert.NotEqual(IntPtr.Zero, pointer);
		Assert.Equal(1, Marshal.ReadByte(pointer, 0));
		Assert.Equal(2, Marshal.ReadByte(pointer, 1));
		Assert.Equal(3, Marshal.ReadByte(pointer, 2));
	}

	/// <summary>
	/// 验证 SafeMemoryHandle_释放后取指针_抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 CreateFromBytes、GetPointer、NotEqual、Dispose、True；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void SafeMemoryHandle_释放后取指针_抛ObjectDisposedException()
	{
		SafeMemoryHandle handle = SafeMemoryHandle.CreateFromBytes(new byte[1] { 9 });
		nint pointer = handle.GetPointer();
		Assert.NotEqual(IntPtr.Zero, pointer);
		handle.Dispose();
		Assert.True(handle.IsClosed);
		Assert.Throws<ObjectDisposedException>(() => handle.GetPointer());
	}

	/// <summary>
	/// 验证 SafeMemoryHandle_传null_抛NullReferenceException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、CreateFromBytes；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void SafeMemoryHandle_传null_抛NullReferenceException()
	{
		Assert.Throws<NullReferenceException>(() => SafeMemoryHandle.CreateFromBytes(null));
	}

	/// <summary>
	/// 验证 SafeMemoryHandle_类型来自BCL而不是宿主程序集 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetName、NotNull、DoesNotContain；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void SafeMemoryHandle_类型来自BCL而不是宿主程序集()
	{
		string name = typeof(SafeHandleZeroOrMinusOneIsInvalid).Assembly.GetName().Name;
		Assert.NotNull(name);
		Assert.DoesNotContain("WindowsBase", name);
		Assert.DoesNotContain("PresentationCore", name);
		Assert.DoesNotContain("PresentationFramework", name);
	}

	/// <summary>
	/// 验证 DynamicDllLoaderEx_路径为空_抛ArgumentNullException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DynamicDllLoaderEx_路径为空_抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new DynamicDllLoaderEx(null));
		Assert.Throws<ArgumentNullException>(() => new DynamicDllLoaderEx(string.Empty));
	}

	/// <summary>
	/// 验证 DynamicDllLoaderEx_加载不存在的DLL_抛DllNotFoundException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DynamicDllLoaderEx_加载不存在的DLL_抛DllNotFoundException()
	{
		DllNotFoundException ex = Assert.Throws<DllNotFoundException>(() => new DynamicDllLoaderEx("__wb_no_such_library__.dll"));
		Assert.Contains("__wb_no_such_library__.dll", ex.Message);
		Assert.Contains("Win32 error code", ex.Message);
	}

	/// <summary>
	/// 验证 DynamicDllLoaderEx_取不存在的导出函数_抛EntryPointNotFoundException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、GetFunction、Contains、Dispose；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DynamicDllLoaderEx_取不存在的导出函数_抛EntryPointNotFoundException()
	{
		DynamicDllLoaderEx loader = new DynamicDllLoaderEx("kernel32.dll");
		try
		{
			EntryPointNotFoundException ex = Assert.Throws<EntryPointNotFoundException>(() => loader.GetFunction<Action>("__WbNoSuchExport__"));
			Assert.Contains("__WbNoSuchExport__", ex.Message);
		}
		finally
		{
			if (loader != null)
			{
				((IDisposable)loader).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 DynamicDllLoaderEx_释放后取函数_抛ObjectDisposedException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Throws、GetFunction；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DynamicDllLoaderEx_释放后取函数_抛ObjectDisposedException()
	{
		DynamicDllLoaderEx loader = new DynamicDllLoaderEx("kernel32.dll");
		loader.Dispose();
		Assert.Throws<ObjectDisposedException>(() => loader.GetFunction<Action>("__WbNoSuchExport__"));
	}

	/// <summary>
	/// 验证 DynamicDllLoaderEx_重复释放_不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Null、Exception、Dispose；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DynamicDllLoaderEx_重复释放_不抛()
	{
		DynamicDllLoaderEx loader = new DynamicDllLoaderEx("kernel32.dll");
		Assert.Null(Record.Exception(delegate
		{
			loader.Dispose();
		}));
		Assert.Null(Record.Exception(delegate
		{
			loader.Dispose();
		}));
	}

	/// <summary>
	/// 验证 CpuMonitorEx_构造与读取_不抛且取值落在0到100 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetSystemCpuUsage、GetProcessCpuUsage、InRange；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CpuMonitorEx_构造与读取_不抛且取值落在0到100()
	{
		using CpuMonitorEx cpuMonitorEx = new CpuMonitorEx();
		float systemCpuUsage = cpuMonitorEx.GetSystemCpuUsage();
		float processCpuUsage = cpuMonitorEx.GetProcessCpuUsage();
		Assert.InRange(systemCpuUsage, 0f, 100f);
		Assert.InRange(processCpuUsage, 0f, 100f);
	}

	/// <summary>
	/// 验证 CpuMonitorEx_重复释放_不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Null、Exception、Dispose；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CpuMonitorEx_重复释放_不抛()
	{
		CpuMonitorEx monitor = new CpuMonitorEx();
		Assert.Null(Record.Exception(delegate
		{
			monitor.Dispose();
		}));
		Assert.Null(Record.Exception(delegate
		{
			monitor.Dispose();
		}));
	}

	/// <summary>
	/// 验证 CpuMonitorEx_释放后读取_不抛且取值仍在0到100 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Dispose、Null、Exception、GetSystemCpuUsage、InRange；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CpuMonitorEx_释放后读取_不抛且取值仍在0到100()
	{
		CpuMonitorEx monitor = new CpuMonitorEx();
		try
		{
			monitor.Dispose();
			Assert.Null(Record.Exception(() => monitor.GetSystemCpuUsage()));
			Assert.InRange(monitor.GetSystemCpuUsage(), 0f, 100f);
			Assert.InRange(monitor.GetProcessCpuUsage(), 0f, 100f);
		}
		finally
		{
			if (monitor != null)
			{
				((IDisposable)monitor).Dispose();
			}
		}
	}

	/// <summary>
	/// 验证 CpuMonitorEx_计数器不可用时的降级_返回0而不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SetValue、GetField、Equal、GetSystemCpuUsage、GetProcessCpuUsage；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void CpuMonitorEx_计数器不可用时的降级_返回0而不抛()
	{
		using CpuMonitorEx cpuMonitorEx = new CpuMonitorEx();
		Type typeFromHandle = typeof(CpuMonitorEx);
		typeFromHandle.GetField("isInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cpuMonitorEx, false);
		Assert.Equal(0f, cpuMonitorEx.GetSystemCpuUsage());
		Assert.Equal(0f, cpuMonitorEx.GetProcessCpuUsage());
	}

	/// <summary>
	/// 验证 ProcessEx_进程不存在_静默返回不抛 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ToString、NewGuid、Empty、GetProcessesByName、Null；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProcessEx_进程不存在_静默返回不抛()
	{
		string absent = "__wb_no_such_process__" + Guid.NewGuid().ToString("N");
		Assert.Empty(Process.GetProcessesByName(absent));
		Assert.Null(Record.Exception(delegate
		{
			ProcessEx.CloseProcess(absent);
		}));
	}

	/// <summary>
	/// 验证 ProcessEx_传null或空串_在碰进程之前就抛异常 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 Throws、CloseProcess、False、IsAssignableFrom、GetType；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProcessEx_传null或空串_在碰进程之前就抛异常()
	{
		Assert.Throws<ArgumentNullException>(delegate
		{
			ProcessEx.CloseProcess(null);
		});
		Assert.Throws<ArgumentException>(delegate
		{
			ProcessEx.CloseProcess(string.Empty);
		});
		Assert.False(typeof(ArgumentNullException).IsAssignableFrom(Record.Exception(delegate
		{
			ProcessEx.CloseProcess(string.Empty);
		}).GetType()));
	}

	/// <summary>
	/// 验证 ProcessEx_源码里必须保留参数校验 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProcessEx_源码里必须保留参数校验()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Platform", "Processes", "ProcessEx.cs"));
		Assert.Contains("processes.Length == 0", actualString);
		Assert.Contains("processName == null", actualString);
		Assert.Contains("processName.Length == 0", actualString);
		Assert.Contains("ArgumentNullException", actualString);
		Assert.Contains("ArgumentException", actualString);
	}

	/// <summary>
	/// 验证 ExtensionSourceRoot 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetDirectoryName、False、IsNullOrEmpty、GetFullPath、Combine；具体条件和先后次序见方法体。
	/// </summary>
	private static string ExtensionSourceRoot([CallerFilePath] string thisFile = "")
	{
		string directoryName = Path.GetDirectoryName(thisFile);
		Assert.False(string.IsNullOrEmpty(directoryName), "无法从 CallerFilePath 解析出测试目录");
		return Path.GetFullPath(Path.Combine(directoryName, "..", "..", "Extension.Core"));
	}
}
