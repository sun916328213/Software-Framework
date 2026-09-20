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

public class CorePlatformTests
{
	[Fact]
	public void FpsEx_初始状态_帧率与总帧数都是0()
	{
		FpsEx fpsEx = new FpsEx();
		Assert.Equal(0.0, fpsEx.CurrentFps);
		Assert.Equal(0, fpsEx.TotalFrameCount);
	}

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

	[Fact]
	public void FpsEx_间隔参数非正数_回落到10()
	{
		FieldInfo field = typeof(FpsEx).GetField("_calculationInterval", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.Equal(10, (int)field.GetValue(new FpsEx(0)));
		Assert.Equal(10, (int)field.GetValue(new FpsEx(-1)));
		Assert.Equal(3, (int)field.GetValue(new FpsEx(3)));
	}

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

	[Fact]
	public void FpsEx_格式化输出()
	{
		FpsEx fpsEx = new FpsEx(2);
		fpsEx.Tick();
		fpsEx.Tick();
		Assert.StartsWith("FPS: ", fpsEx.GetFormattedFps());
		Assert.Contains("(Total: 2)", fpsEx.GetDetailedFpsInfo());
	}

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

	[Fact]
	public void SafeMemoryHandle_传null_抛NullReferenceException()
	{
		Assert.Throws<NullReferenceException>(() => SafeMemoryHandle.CreateFromBytes(null));
	}

	[Fact]
	public void SafeMemoryHandle_类型来自BCL而不是宿主程序集()
	{
		string name = typeof(SafeHandleZeroOrMinusOneIsInvalid).Assembly.GetName().Name;
		Assert.NotNull(name);
		Assert.DoesNotContain("WindowsBase", name);
		Assert.DoesNotContain("PresentationCore", name);
		Assert.DoesNotContain("PresentationFramework", name);
	}

	[Fact]
	public void DynamicDllLoaderEx_路径为空_抛ArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => new DynamicDllLoaderEx(null));
		Assert.Throws<ArgumentNullException>(() => new DynamicDllLoaderEx(string.Empty));
	}

	[Fact]
	public void DynamicDllLoaderEx_加载不存在的DLL_抛DllNotFoundException()
	{
		DllNotFoundException ex = Assert.Throws<DllNotFoundException>(() => new DynamicDllLoaderEx("__wb_no_such_library__.dll"));
		Assert.Contains("__wb_no_such_library__.dll", ex.Message);
		Assert.Contains("Win32 error code", ex.Message);
	}

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

	[Fact]
	public void DynamicDllLoaderEx_释放后取函数_抛ObjectDisposedException()
	{
		DynamicDllLoaderEx loader = new DynamicDllLoaderEx("kernel32.dll");
		loader.Dispose();
		Assert.Throws<ObjectDisposedException>(() => loader.GetFunction<Action>("__WbNoSuchExport__"));
	}

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

	[Fact]
	public void CpuMonitorEx_构造与读取_不抛且取值落在0到100()
	{
		using CpuMonitorEx cpuMonitorEx = new CpuMonitorEx();
		float systemCpuUsage = cpuMonitorEx.GetSystemCpuUsage();
		float processCpuUsage = cpuMonitorEx.GetProcessCpuUsage();
		Assert.InRange(systemCpuUsage, 0f, 100f);
		Assert.InRange(processCpuUsage, 0f, 100f);
	}

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

	[Fact]
	public void CpuMonitorEx_计数器不可用时的降级_返回0而不抛()
	{
		using CpuMonitorEx cpuMonitorEx = new CpuMonitorEx();
		Type typeFromHandle = typeof(CpuMonitorEx);
		typeFromHandle.GetField("isInitialized", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cpuMonitorEx, false);
		Assert.Equal(0f, cpuMonitorEx.GetSystemCpuUsage());
		Assert.Equal(0f, cpuMonitorEx.GetProcessCpuUsage());
	}

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

	private static string ExtensionSourceRoot([CallerFilePath] string thisFile = "")
	{
		string directoryName = Path.GetDirectoryName(thisFile);
		Assert.False(string.IsNullOrEmpty(directoryName), "无法从 CallerFilePath 解析出测试目录");
		return Path.GetFullPath(Path.Combine(directoryName, "..", "Extension"));
	}
}
