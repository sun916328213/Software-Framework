using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Extension.UI.Controls;
using Xunit;

namespace Extension.Tests;

public class UiControlsPlaceholderTests
{
	private const string PlaceholderFileName = "DataGridViewEx.cs";

	private const string UiControlsNamespace = "Extension.UI.Controls";

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

	[Fact]
	public void Extension工程是UseWPF的_而该文件自称Winforms扩展()
	{
		string text = SourcePath("Extension.csproj");
		Assert.True(File.Exists(text), "找不到工程文件：" + text);
		Assert.Contains("<UseWPF>true</UseWPF>", File.ReadAllText(text));
		string actualString = File.ReadAllText(SourcePath("UI", "Controls", "DataGridViewEx.cs"));
		Assert.Contains("Winforms", actualString);
	}

	[Fact]
	public void DataGridViewEx_没有任何宿主或第三方using()
	{
		string actualString = File.ReadAllText(SourcePath("UI", "Controls", "DataGridViewEx.cs"));
		Assert.DoesNotContain("using System.Windows", actualString);
		Assert.DoesNotContain("using Microsoft.Win32", actualString);
		Assert.DoesNotContain("using MaterialDesign", actualString);
	}

	private static string ThisFile([CallerFilePath] string path = "")
	{
		return path;
	}

	private static string SourcePath(params string[] segments)
	{
		string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ThisFile()), "..", "Extension"));
		string text = fullPath;
		foreach (string path in segments)
		{
			text = Path.Combine(text, path);
		}
		return text;
	}
}
