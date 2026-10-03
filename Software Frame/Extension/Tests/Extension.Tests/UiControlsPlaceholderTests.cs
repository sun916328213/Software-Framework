// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：UiControlsPlaceholderTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Linq、System.Runtime.CompilerServices、Extension.UI.Controls、Xunit。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Extension.UI.Controls;
using Xunit;

namespace Extension.Tests;

/// <summary>
/// UiControlsPlaceholderTests 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class UiControlsPlaceholderTests
{
	private const string PlaceholderFileName = "DataGridViewEx.cs";

	private const string UiControlsNamespace = "Extension.UI.Controls";

	/// <summary>
	/// 验证 Ui_Controls命名空间里没有DataGridView相关类型 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ToArray、GetTypes、NotEmpty、Contains、DoesNotContain；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Ui_Controls命名空间里没有DataGridView相关类型()
	{
		string[] collection = (from t in typeof(DelayEx).Assembly.GetTypes()
			where t.Namespace == "Extension.UI.Controls"
			select t.Name).ToArray();
		Assert.NotEmpty(collection);
		Assert.Contains("DelayEx", collection);
		Assert.DoesNotContain(collection, (string name) => name.IndexOf("DataGridView", StringComparison.Ordinal) >= 0);
	}

	/// <summary>
	/// 验证 DataGridViewEx_有效代码只有using与命名空间声明 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SourcePath、True、Exists、ToArray、ReadAllLines；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DataGridViewEx_有效代码只有using与命名空间声明()
	{
		string text = SourcePath("UI", "Controls", "DataGridViewEx.cs");
		Assert.True(File.Exists(text), "找不到源码文件：" + text);
		string[] array = (from line in File.ReadAllLines(text)
			select line.Trim().TrimStart('\ufeff').Trim() into line
			where line.Length > 0 && !line.StartsWith("//")
			select line).ToArray();
		Assert.Contains("using System.Reflection;", array);
		Assert.Contains("namespace Extension.UI.Controls", array);
		string actualString = string.Join("\n", array);
		Assert.DoesNotContain("class", actualString);
		Assert.DoesNotContain("interface", actualString);
		Assert.DoesNotContain("struct", actualString);
		Assert.DoesNotContain("enum", actualString);
		Assert.DoesNotContain("public", actualString);
		Assert.DoesNotContain("private", actualString);
		Assert.DoesNotContain("void", actualString);
	}

	/// <summary>
	/// 验证 Extension工程是UseWPF的_而该文件自称Winforms扩展 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 SourcePath、True、Exists、Contains、ReadAllText；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void Extension工程是UseWPF的_而该文件自称Winforms扩展()
	{
		string text = SourcePath("Extension.csproj");
		Assert.True(File.Exists(text), "找不到工程文件：" + text);
		Assert.Contains("<UseWPF>true</UseWPF>", File.ReadAllText(text));
		string actualString = File.ReadAllText(SourcePath("UI", "Controls", "DataGridViewEx.cs"));
		Assert.Contains("Winforms", actualString);
	}

	/// <summary>
	/// 验证 DataGridViewEx_没有任何宿主或第三方using 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 ReadAllText、SourcePath、DoesNotContain；具体条件和先后次序见方法体。
	/// </summary>
	[Fact]
	public void DataGridViewEx_没有任何宿主或第三方using()
	{
		string actualString = File.ReadAllText(SourcePath("UI", "Controls", "DataGridViewEx.cs"));
		Assert.DoesNotContain("using System.Windows", actualString);
		Assert.DoesNotContain("using Microsoft.Win32", actualString);
		Assert.DoesNotContain("using MaterialDesign", actualString);
	}

	/// <summary>
	/// 验证 ThisFile 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// </summary>
	private static string ThisFile([CallerFilePath] string path = "")
	{
		return path;
	}

	/// <summary>
	/// 验证 SourcePath 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
	/// 正文中的关键协作调用包括 GetFullPath、Combine、GetDirectoryName、ThisFile；具体条件和先后次序见方法体。
	/// </summary>
	private static string SourcePath(params string[] segments)
	{
		string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "..", "Extension"));
		string text = fullPath;
		foreach (string path in segments)
		{
			text = Path.Combine(text, path);
		}
		return text;
	}
}
