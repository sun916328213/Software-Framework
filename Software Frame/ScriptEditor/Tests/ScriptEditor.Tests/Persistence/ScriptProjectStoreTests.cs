// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ScriptProjectStoreTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Text.Json、ScriptEditor.Core.Models、ScriptEditor.Runtime.Persistence、ScriptEditor.Runtime.Services、Xunit。导入不代表所有类型都会在运行时加载。
using System.Text.Json;
using ScriptEditor.Core.Models;
using ScriptEditor.Runtime.Persistence;
using ScriptEditor.Runtime.Services;
using Xunit;

namespace ScriptEditor.Tests.Persistence;

/// <summary>项目迁移与持久化：V1→V2、往返稳定、相对引用、镜像清单（无 WPF）。</summary>
public sealed class ScriptProjectStoreTests : IDisposable
{
    private const string LegacyJson = """
        {"Name":"legacy","References":["shared-driver.dll"],"Scripts":[
          {"Title":"one","Code":"using System.Text;\nConsole.WriteLine(1);","References":["missing-driver.dll"],"Breakpoints":[3]},
          {"Title":"two","Code":"Console.WriteLine(2);","References":[]}
        ]}
        """;

    private readonly List<string> _tempDirs = new();
    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// 测试相关入口 Dispose；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Delete；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* 忽略 */ }
        }
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, true); } catch { /* 忽略 */ }
        }
    }

    /// <summary>
    /// 测试相关入口 NewTempDir；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、NewGuid、CreateDirectory、Add；具体条件和先后次序见方法体。
    /// </summary>
    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"se-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    /// <summary>
    /// 测试相关入口 MigrateV1KeepsScriptsAndReferences；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Migrate、NotNull、Equal、NotEqual、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MigrateV1KeepsScriptsAndReferences()
    {
        var dto = ScriptProjectMigration.Migrate(LegacyJson);
        Assert.NotNull(dto);
        Assert.Equal(ScriptProjectMigration.CurrentFormatVersion, dto.FormatVersion);
        Assert.Equal(2, dto.Scripts.Count);
        Assert.NotEqual(Guid.Empty, dto.Scripts[0].ScriptId);
        Assert.NotEqual(dto.Scripts[0].ScriptId, dto.Scripts[1].ScriptId);
        Assert.Single(dto.Scripts[0].References);
        Assert.Equal("missing-driver.dll", dto.Scripts[0].References[0].Path);
        Assert.Equal(new[] { 3 }, dto.Scripts[0].Breakpoints);
        Assert.Single(dto.References);
    }

    /// <summary>
    /// 测试相关入口 MigrateInvalidReturnsNull；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Null、Migrate；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void MigrateInvalidReturnsNull()
    {
        Assert.Null(ScriptProjectMigration.Migrate(string.Empty));
        Assert.Null(ScriptProjectMigration.Migrate("not json"));
        Assert.Null(ScriptProjectMigration.Migrate("""{"Name":"empty"}"""));
    }

    /// <summary>
    /// 测试相关入口 ModelRoundTripKeepsIds；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ToModel、FromModel、Equal、Single；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ModelRoundTripKeepsIds()
    {
        var project = new ScriptProject
        {
            Name = "roundtrip",
            References = new List<ScriptReference> { new(@"C:\libs\a.dll", "a.dll") },
            Scripts = new List<ScriptProjectItem>
            {
                new() { Title = "s1", Code = "Console.WriteLine(1);", Breakpoints = new List<int> { 2 } }
            }
        };
        var back = ScriptProjectMigration.ToModel(
            ScriptProjectMigration.FromModel(project, @"C:\tasks\demo"), @"C:\tasks\demo");
        Assert.Equal("roundtrip", back.Name);
        Assert.Single(back.Scripts);
        Assert.Equal(project.Scripts[0].ScriptId, back.Scripts[0].ScriptId);
        Assert.Equal(new[] { 2 }, back.Scripts[0].Breakpoints);
        Assert.Equal(@"C:\libs\a.dll", back.References[0].Path);
    }

    /// <summary>
    /// 测试相关入口 StoreSavesRelativeAndLoadsAbsolute；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewTempDir、Combine、CreateDirectory、WriteAllBytesAsync、SaveAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StoreSavesRelativeAndLoadsAbsolute()
    {
        var dir = NewTempDir();
        var libs = Path.Combine(dir, "libs");
        Directory.CreateDirectory(libs);
        var dllPath = Path.Combine(libs, "driver.dll");
        await File.WriteAllBytesAsync(dllPath, new byte[] { 0x4D, 0x5A });

        var project = new ScriptProject
        {
            Name = "rel",
            Scripts = new List<ScriptProjectItem>
            {
                new()
                {
                    Title = "s",
                    Code = "Console.WriteLine(1);",
                    References = new List<ScriptReference> { new(dllPath, "driver.dll") }
                }
            }
        };
        var store = new ScriptProjectStore();
        var taskPath = Path.Combine(dir, "job.taskproj");
        await store.SaveAsync(taskPath, project);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(taskPath));
        var savedPath = document.RootElement.GetProperty("Scripts")[0]
            .GetProperty("References")[0].GetProperty("Path").GetString();
        Assert.Equal("libs" + Path.DirectorySeparatorChar + "driver.dll", savedPath);

        var loaded = await store.LoadAsync(taskPath);
        Assert.Single(loaded.Scripts);
        Assert.Equal(dllPath, loaded.Scripts[0].References[0].Path);
    }

    /// <summary>
    /// 测试相关入口 StoreLoadsLegacyFile；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、GetTempPath、NewGuid、Add、WriteAllTextAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StoreLoadsLegacyFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"se-legacy-{Guid.NewGuid():N}.taskproj");
        _tempFiles.Add(path);
        await File.WriteAllTextAsync(path, LegacyJson);
        var loaded = await new ScriptProjectStore().LoadAsync(path);
        Assert.Equal(2, loaded.Scripts.Count);
        Assert.Equal(new[] { 3 }, loaded.Scripts[0].Breakpoints);
    }

    /// <summary>
    /// 测试相关入口 StoreRotatesBackup；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewTempDir、Combine、SaveAsync、True、Exists；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task StoreRotatesBackup()
    {
        var dir = NewTempDir();
        var taskPath = Path.Combine(dir, "job.taskproj");
        var store = new ScriptProjectStore();
        var project = new ScriptProject { Name = "v1" };
        await store.SaveAsync(taskPath, project);
        project.Name = "v2";
        await store.SaveAsync(taskPath, project);

        var backup = taskPath + ".bak";
        Assert.True(File.Exists(backup));
        var reloaded = await store.LoadAsync(taskPath);
        Assert.Equal("v2", reloaded.Name);
        var backupLoaded = await store.LoadAsync(backup);
        Assert.Equal("v1", backupLoaded.Name);
    }

    /// <summary>
    /// 测试相关入口 CheckedInSampleLoadsAndRuns；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Combine、True、Exists、LoadAsync、Equal；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task CheckedInSampleLoadsAndRuns()
    {
        var sample = Path.Combine(AppContext.BaseDirectory, "Samples", "Sample.taskproj");
        Assert.True(File.Exists(sample), $"示例文件缺失: {sample}");
        var project = await new ScriptProjectStore().LoadAsync(sample);
        Assert.Equal("示例任务", project.Name);
        Assert.Equal(2, project.Scripts.Count);

        var result = await RoslynScriptRuntime.Shared.ExecuteAsync(project.Scripts[0]);
        Assert.True(result.Success, result.Status);
        Assert.Contains("hello-sample", result.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 测试相关入口 ManifestRoundTripsIdentity；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 NewGuid、Add、Sha256Short、NewTempDir、Save；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ManifestRoundTripsIdentity()
    {
        var manifest = new ScriptFileManifest();
        var id = Guid.NewGuid();
        manifest.Files.Add(new ScriptFileManifestEntry
        {
            FileName = "01_alpha.cs",
            ScriptId = id,
            ContentHash = ContentHash.Sha256Short("Console.WriteLine(1);")
        });

        var dir = NewTempDir();
        manifest.Save(dir);
        var loaded = ScriptFileManifest.Load(dir);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Files);
        Assert.Equal(id, loaded.Files[0].ScriptId);
        Assert.NotEqual(
            loaded.Files[0].ContentHash,
            ContentHash.Sha256Short("Console.WriteLine(2);"));
    }

    /// <summary>
    /// 测试相关入口 ContentHashIsStable；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Sha256Short、NotEqual；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ContentHashIsStable()
    {
        Assert.Equal(ContentHash.Sha256Short("abc"), ContentHash.Sha256Short("abc"));
        Assert.NotEqual(ContentHash.Sha256Short("abc"), ContentHash.Sha256Short("abd"));
    }
}
