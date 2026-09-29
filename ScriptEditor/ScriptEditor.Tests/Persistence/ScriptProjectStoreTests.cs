using System.Text.Json;
using ScriptEditor.Models;
using ScriptEditor.Persistence;
using ScriptEditor.Services;
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

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"se-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

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

    [Fact]
    public void MigrateInvalidReturnsNull()
    {
        Assert.Null(ScriptProjectMigration.Migrate(string.Empty));
        Assert.Null(ScriptProjectMigration.Migrate("not json"));
        Assert.Null(ScriptProjectMigration.Migrate("""{"Name":"empty"}"""));
    }

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

    [Fact]
    public void ContentHashIsStable()
    {
        Assert.Equal(ContentHash.Sha256Short("abc"), ContentHash.Sha256Short("abc"));
        Assert.NotEqual(ContentHash.Sha256Short("abc"), ContentHash.Sha256Short("abd"));
    }
}
