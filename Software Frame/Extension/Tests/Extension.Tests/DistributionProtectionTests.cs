// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DistributionProtectionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Diagnostics、System.IO、System.Linq、System.Runtime.CompilerServices。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Extension.Security.Cryptography;
using Extension.Security.Protection;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// DistributionProtectionTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class DistributionProtectionTests
{
	/// <summary>
	/// 测试相关入口 AntiDebugEx_IsDebuggerAttached与ThrowIf结论必须一致；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 IsDebuggerAttached、Exception、ThrowIfDebuggerAttached、IsType、Null；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiDebugEx_IsDebuggerAttached与ThrowIf结论必须一致()
	{
		bool flag = AntiDebugEx.IsDebuggerAttached();
		Exception ex = Record.Exception(delegate
		{
			AntiDebugEx.ThrowIfDebuggerAttached();
		});
		if (flag)
		{
			Assert.IsType<InvalidOperationException>(ex);
		}
		else
		{
			Assert.Null(ex);
		}
	}

	/// <summary>
	/// 测试相关入口 AntiDebugEx_计时陷阱的阈值非正数直接抛_不再恒真误报；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 Throws、IsTimingAnomaly、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiDebugEx_计时陷阱的阈值非正数直接抛_不再恒真误报()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => AntiDebugEx.IsTimingAnomaly(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => AntiDebugEx.IsTimingAnomaly(-1));
		Assert.Throws<ArgumentOutOfRangeException>(() => AntiDebugEx.IsTimingAnomaly(int.MinValue));
		Assert.False(AntiDebugEx.IsTimingAnomaly(int.MaxValue));
		Assert.False(AntiDebugEx.IsTimingAnomaly());
	}

	/// <summary>
	/// 测试相关入口 AntiDebugEx_计时陷阱的阈值低于Sleep时长仍恒真_已记录未修；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 True、IsTimingAnomaly；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiDebugEx_计时陷阱的阈值低于Sleep时长仍恒真_已记录未修()
	{
		Assert.True(AntiDebugEx.IsTimingAnomaly(1));
		Assert.True(AntiDebugEx.IsTimingAnomaly(50));
	}

	/// <summary>
	/// 测试相关入口 AntiDebugEx_三个Win32探测点都必须吞环境异常并留痕；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Equal、Split；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiDebugEx_三个Win32探测点都必须吞环境异常并留痕()
	{
		string text = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Protection", "AntiDebugEx.cs"));
		Assert.Equal(3, text.Split("Debug.WriteLine").Length - 1);
		Assert.Contains("IsProbeUnsupported", text);
		Assert.Contains("DllNotFoundException", text);
		Assert.Contains("EntryPointNotFoundException", text);
		Assert.Contains("Win32Exception", text);
		Assert.Contains("DllImport(\"kernel32.dll\")", text);
		Assert.Contains("DllImport(\"ntdll.dll\")", text);
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_入口程序集哈希_自己算的能自己校验通过；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 GetEntryAssemblyHash、Equal、Matches、True、VerifyEntryAssembly；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_入口程序集哈希_自己算的能自己校验通过()
	{
		string entryAssemblyHash = AntiTamperEx.GetEntryAssemblyHash();
		Assert.Equal(64, entryAssemblyHash.Length);
		Assert.Matches("^[0-9a-f]{64}$", entryAssemblyHash);
		Assert.True(AntiTamperEx.VerifyEntryAssembly(entryAssemblyHash));
		Assert.True(AntiTamperEx.VerifyEntryAssembly(entryAssemblyHash.ToUpperInvariant()));
		Assert.True(AntiTamperEx.VerifyEntryAssembly("  " + entryAssemblyHash + "  "));
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_VerifyEntryAssembly对空期望值返回false；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 False、VerifyEntryAssembly；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_VerifyEntryAssembly对空期望值返回false()
	{
		Assert.False(AntiTamperEx.VerifyEntryAssembly(null));
		Assert.False(AntiTamperEx.VerifyEntryAssembly(string.Empty));
		Assert.False(AntiTamperEx.VerifyEntryAssembly("   "));
		Assert.False(AntiTamperEx.VerifyEntryAssembly(new string('0', 64)));
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_VerifyFile_不存在返回false_存在时按哈希比对；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 WriteFile、Sha256File、True、VerifyFile、ToUpperInvariant；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_VerifyFile_不存在返回false_存在时按哈希比对()
	{
		using TempWorkspace tempWorkspace = new TempWorkspace("tamper");
		string path = tempWorkspace.WriteFile("a.txt", "abc");
		string text = HashEx.Sha256File(path);
		Assert.True(AntiTamperEx.VerifyFile(path, text));
		Assert.True(AntiTamperEx.VerifyFile(path, text.ToUpperInvariant()));
		Assert.False(AntiTamperEx.VerifyFile(path, new string('0', 64)));
		Assert.False(AntiTamperEx.VerifyFile(tempWorkspace.Combine("nope.txt"), text));
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_两个校验方法对null期望值的处理已对齐；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 WriteFile、False、VerifyEntryAssembly、VerifyFile；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_两个校验方法对null期望值的处理已对齐()
	{
		using TempWorkspace tempWorkspace = new TempWorkspace("tamper_null");
		string path = tempWorkspace.WriteFile("a.txt", "x");
		Assert.False(AntiTamperEx.VerifyEntryAssembly(null));
		Assert.False(AntiTamperEx.VerifyFile(path, null));
		Assert.False(AntiTamperEx.VerifyFile(path, string.Empty));
		Assert.False(AntiTamperEx.VerifyFile(path, "   "));
		Assert.False(AntiTamperEx.VerifyFile(null, new string('0', 64)));
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_源码里VerifyFile必须保留期望值空值短路；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_源码里VerifyFile必须保留期望值空值短路()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Protection", "AntiTamperEx.cs"));
		Assert.Contains("if (string.IsNullOrWhiteSpace(expectedSha256Hex)) return false;", actualString);
	}

	/// <summary>
	/// 测试相关入口 AntiTamperEx_ildasm抑制检查_按特性全名做字符串比较；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Contains、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiTamperEx_ildasm抑制检查_按特性全名做字符串比较()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Protection", "AntiTamperEx.cs"));
		Assert.Contains("\"System.Runtime.CompilerServices.SuppressIldasmAttribute\"", actualString);
		Assert.False(AntiTamperEx.IsIldasmSuppressed(typeof(AntiTamperEx).Assembly));
		Assert.False(AntiTamperEx.IsIldasmSuppressed());
	}

	/// <summary>
	/// 测试相关入口 AntiToolProcessEx_名单是27项小写且不含exe后缀；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 Equal、All、DoesNotContain、ToLowerInvariant、False；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiToolProcessEx_名单是27项小写且不含exe后缀()
	{
		string[] suspiciousProcessNames = AntiToolProcessEx.SuspiciousProcessNames;
		Assert.Equal(27, suspiciousProcessNames.Length);
		Assert.All(suspiciousProcessNames, delegate(string n)
		{
			Assert.DoesNotContain(".exe", n);
		});
		Assert.All(suspiciousProcessNames, delegate(string n)
		{
			Assert.Equal(n, n.ToLowerInvariant());
		});
		Assert.All(suspiciousProcessNames, delegate(string n)
		{
			Assert.False(string.IsNullOrWhiteSpace(n));
		});
		Assert.Equal(suspiciousProcessNames.Length, suspiciousProcessNames.Distinct().Count());
	}

	/// <summary>
	/// 测试相关入口 AntiToolProcessEx_枚举是只读操作_返回的Process必须由调用方释放；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 GetSuspiciousProcesses、True、GetType、NotNull、Dispose；具体条件和先后次序见方法体。
	/// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
	/// </summary>
	[Fact]
	public void AntiToolProcessEx_枚举是只读操作_返回的Process必须由调用方释放()
	{
		IReadOnlyList<Process> suspiciousProcesses;
		try
		{
			suspiciousProcesses = AntiToolProcessEx.GetSuspiciousProcesses();
		}
		catch (Exception ex)
		{
			Assert.True(condition: false, "只读枚举不应抛异常，实际：" + ex.GetType().Name + ": " + ex.Message);
			return;
		}
		try
		{
			Assert.NotNull(suspiciousProcesses);
		}
		finally
		{
			foreach (Process item in suspiciousProcesses)
			{
				item.Dispose();
			}
		}
	}

	/// <summary>
	/// 测试相关入口 AntiToolProcessEx_命中读取进程名时只吞进程已退出这一种异常；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AntiToolProcessEx_命中读取进程名时只吞进程已退出这一种异常()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Protection", "AntiToolProcessEx.cs"));
		Assert.Contains("catch (InvalidOperationException ex)", actualString);
		Assert.Contains("Debug.WriteLine", actualString);
		Assert.Contains("Distinct(StringComparer.OrdinalIgnoreCase)", actualString);
	}

	/// <summary>
	/// 测试相关入口 ProtectionOptions_默认值_默认就是抛异常且不轮询；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 True、False、Null、Equal；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void ProtectionOptions_默认值_默认就是抛异常且不轮询()
	{
		ProtectionOptions protectionOptions = new ProtectionOptions();
		Assert.True(protectionOptions.CheckDebugger);
		Assert.True(protectionOptions.CheckSuspiciousTools);
		Assert.False(protectionOptions.CheckIntegrity);
		Assert.Null(protectionOptions.ExpectedEntryHash);
		Assert.Equal(ThreatAction.Throw, protectionOptions.Action);
		Assert.Equal(TimeSpan.Zero, protectionOptions.PollInterval);
	}

	/// <summary>
	/// 测试相关入口 AppProtectionEx_三项检查全关时Protect不抛且可反复启停；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 StopWatching、Null、Exception、Protect、FromMilliseconds；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AppProtectionEx_三项检查全关时Protect不抛且可反复启停()
	{
		AppProtectionEx.StopWatching();
		ProtectionOptions o = new ProtectionOptions
		{
			CheckDebugger = false,
			CheckSuspiciousTools = false,
			CheckIntegrity = false,
			Action = ThreatAction.NotifyOnly
		};
		try
		{
			Assert.Null(Record.Exception(delegate
			{
				AppProtectionEx.Protect(o);
			}));
			Assert.Null(Record.Exception(delegate
			{
				AppProtectionEx.Protect(new ProtectionOptions
				{
					CheckDebugger = false,
					CheckSuspiciousTools = false,
					CheckIntegrity = false,
					Action = ThreatAction.NotifyOnly,
					PollInterval = TimeSpan.FromMilliseconds(50L, 0L)
				});
			}));
		}
		finally
		{
			AppProtectionEx.StopWatching();
		}
		Assert.Null(Record.Exception(delegate
		{
			AppProtectionEx.StopWatching();
		}));
	}

	/// <summary>
	/// 测试相关入口 AppProtectionEx_开了完整性校验但没给期望哈希_会静默跳过；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 Protect、False、StopWatching；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AppProtectionEx_开了完整性校验但没给期望哈希_会静默跳过()
	{
		bool fired = false;
		Action<ThreatType, string> value = delegate
		{
			fired = true;
		};
		AppProtectionEx.OnThreatDetected += value;
		try
		{
			AppProtectionEx.Protect(new ProtectionOptions
			{
				CheckDebugger = false,
				CheckSuspiciousTools = false,
				CheckIntegrity = true,
				ExpectedEntryHash = null,
				Action = ThreatAction.NotifyOnly
			});
			Assert.False(fired);
		}
		finally
		{
			AppProtectionEx.OnThreatDetected -= value;
			AppProtectionEx.StopWatching();
		}
	}

	/// <summary>
	/// 测试相关入口 AppProtectionEx_完整性校验失败时NotifyOnly只触发事件不中断；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 Add、NotEqual、Protect、Equal、StopWatching；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AppProtectionEx_完整性校验失败时NotifyOnly只触发事件不中断()
	{
		List<ThreatType> seen = new List<ThreatType>();
		Action<ThreatType, string> value = delegate(ThreatType t, string m)
		{
			seen.Add(t);
			Assert.NotEqual(string.Empty, m);
		};
		AppProtectionEx.OnThreatDetected += value;
		try
		{
			AppProtectionEx.Protect(new ProtectionOptions
			{
				CheckDebugger = false,
				CheckSuspiciousTools = false,
				CheckIntegrity = true,
				ExpectedEntryHash = new string('0', 64),
				Action = ThreatAction.NotifyOnly
			});
			Assert.Equal(new ThreatType[1] { ThreatType.Tampered }, seen);
		}
		finally
		{
			AppProtectionEx.OnThreatDetected -= value;
			AppProtectionEx.StopWatching();
		}
	}

	/// <summary>
	/// 测试相关入口 AppProtectionEx_Exit动作会直接杀进程_测试里绝不能让它命中；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、Combine、ExtensionSourceRoot、Contains；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void AppProtectionEx_Exit动作会直接杀进程_测试里绝不能让它命中()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Protection", "AppProtectionEx.cs"));
		Assert.Contains("Environment.Exit(unchecked((int)0xDEAD0001))", actualString);
		Assert.Contains("catch (Exception ex)", actualString);
		Assert.Contains("Debug.WriteLine", actualString);
	}

	/// <summary>
	/// 测试相关入口 ExtensionSourceRoot；准备条件、协作调用与结果检查以方法正文为准。
	/// 正文中的关键协作调用包括 GetDirectoryName、False、IsNullOrEmpty、GetFullPath、Combine；具体条件和先后次序见方法体。
	/// </summary>
	private static string ExtensionSourceRoot([CallerFilePath] string thisFile = "")
	{
		string directoryName = Path.GetDirectoryName(thisFile);
		Assert.False(string.IsNullOrEmpty(directoryName), "无法从 CallerFilePath 解析出测试目录");
		return Path.GetFullPath(Path.Combine(directoryName, "..", "..", "Extension.Core"));
	}
}
