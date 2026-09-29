using Script.Tests.Core;
using ScriptEditor.Models;
using ScriptEditor.Options;
using ScriptEditor.Persistence;
using ScriptEditor.Services;
using System.IO;

namespace Script.Tests.CoreTests;

/// <summary>P5 自动化测试：版本化持久化、V1→V2 迁移、相对引用、镜像清单、任务隔离。</summary>
public static class ScriptProjectStoreP5Tests
{
    private const string LegacyJson = """
        {"Name":"legacy","References":["shared-driver.dll"],"Scripts":[
          {"Title":"one","Code":"using System.Text;\nConsole.WriteLine(1);","References":["missing-driver.dll"],"Breakpoints":[3]},
          {"Title":"two","Code":"Console.WriteLine(2);","References":[]}
        ]}
        """;

    public static void Register(TestRunner runner)
    {
        const string cat = "P5 持久化与隔离";

        runner.Register(cat, "V1 迁移保留脚本与引用", ctx =>
        {
            var dto = ScriptProjectMigration.Migrate(LegacyJson);
            ctx.Assert(dto != null, "旧格式应迁移成功");
            if (dto == null) return;
            ctx.Assert(dto.FormatVersion == ScriptProjectMigration.CurrentFormatVersion, $"应升级到 V{ScriptProjectMigration.CurrentFormatVersion}");
            ctx.Assert(dto.Scripts.Count == 2, "应保留两个脚本");
            ctx.Assert(dto.Scripts[0].ScriptId != Guid.Empty && dto.Scripts[0].ScriptId != dto.Scripts[1].ScriptId, "应分配独立标识");
            ctx.Assert(dto.Scripts[0].References.Count == 1 && dto.Scripts[0].References[0].Path == "missing-driver.dll", "缺失 DLL 路径应保留");
            ctx.Assert(dto.Scripts[0].Breakpoints.SequenceEqual(new[] { 3 }), "断点应保留");
            ctx.Assert(dto.References.Count == 1 && dto.References[0].Path == "shared-driver.dll", "全局引用应保留");
        });

        runner.Register(cat, "V2 直读往返稳定", ctx =>
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
            var dto = ScriptProjectMigration.FromModel(project, @"C:\tasks\demo");
            var back = ScriptProjectMigration.ToModel(dto, @"C:\tasks\demo");
            ctx.Assert(back.Name == "roundtrip", "任务名往返");
            ctx.Assert(back.Scripts.Count == 1 && back.Scripts[0].ScriptId == project.Scripts[0].ScriptId, "ScriptId 往返稳定");
            ctx.Assert(back.Scripts[0].Breakpoints.SequenceEqual(new[] { 2 }), "断点往返");
            ctx.Assert(back.References.Count == 1 && back.References[0].Path == @"C:\libs\a.dll", "工程外引用保持绝对路径");
        });

        runner.Register(cat, "引用路径相对工程目录往返", async ctx =>
        {
            var dir = Path.Combine(Path.GetTempPath(), $"p5-store-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var dllDir = Path.Combine(dir, "libs");
                Directory.CreateDirectory(dllDir);
                var dllPath = Path.Combine(dllDir, "driver.dll");
                await File.WriteAllBytesAsync(dllPath, new byte[] { 0x4D, 0x5A });
                var project = new ScriptProject
                {
                    Name = "rel",
                    Scripts = new List<ScriptProjectItem>
                    {
                        new() { Title = "s", Code = "Console.WriteLine(1);",
                            References = new List<ScriptReference> { new(dllPath, "driver.dll") } }
                    }
                };
                var store = new ScriptProjectStore();
                var taskPath = Path.Combine(dir, "job.taskproj");
                await store.SaveAsync(taskPath, project);
                var raw = await File.ReadAllTextAsync(taskPath);
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                var savedPath = doc.RootElement.GetProperty("Scripts")[0]
                    .GetProperty("References")[0].GetProperty("Path").GetString();
                ctx.Assert(savedPath == "libs" + Path.DirectorySeparatorChar + "driver.dll",
                    $"应保存相对路径，实际: {savedPath}");
                var loaded = await store.LoadAsync(taskPath);
                ctx.Assert(loaded.Scripts.Count == 1, "应加载一个脚本");
                ctx.Assert(string.Equals(loaded.Scripts[0].References[0].Path, dllPath, StringComparison.OrdinalIgnoreCase),
                    $"应解析回绝对路径: {loaded.Scripts[0].References[0].Path}");
            }
            finally { Directory.Delete(dir, true); }
        });

        runner.Register(cat, "存储加载旧文件兼容", async ctx =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"p5-legacy-{Guid.NewGuid():N}.taskproj");
            try
            {
                await File.WriteAllTextAsync(path, LegacyJson);
                var loaded = await new ScriptProjectStore().LoadAsync(path);
                ctx.Assert(loaded.Scripts.Count == 2, "旧文件应加载两个脚本");
                ctx.Assert(loaded.Scripts[0].Breakpoints.SequenceEqual(new[] { 3 }), "旧断点应保留");
            }
            finally { File.Delete(path); }
        });

        runner.Register(cat, "任务上下文隐式 using 互不污染", ctx =>
        {
            var ctxA = new ScriptProjectContext { Name = "A" };
            var ctxB = new ScriptProjectContext { Name = "B" };
            ctxA.AddSources(new[] { "using System.Text;\nConsole.WriteLine(1);" });
            ctxB.AddSources(new[] { "using System.IO;\nConsole.WriteLine(2);" });
            var a = ctxA.ResolveUsings();
            var b = ctxB.ResolveUsings();
            ctx.Assert(a.Contains("System.Text") && !a.Contains("System.IO"), $"A 应只含自己的集合: {string.Join(",", a)}");
            ctx.Assert(b.Contains("System.IO") && !b.Contains("System.Text"), $"B 应只含自己的集合: {string.Join(",", b)}");
        });

        runner.Register(cat, "会话使用任务上下文编译执行", async ctx =>
        {
            var context = new ScriptProjectContext { Name = "ctx-run" };
            context.AddSources(new[] { "using System.Text;\n" });
            var session = new ScriptSession(
                () => "var sb = new StringBuilder(); sb.Append(\"ctx-ok\"); Console.WriteLine(sb.ToString());")
            {
                ProjectContext = context
            };
            try
            {
                var ok = await session.RunAsync();
                ctx.Assert(ok, $"上下文 using 应生效: {session.StatusText}/{session.Output}");
                ctx.Assert(session.Output.Contains("ctx-ok", StringComparison.Ordinal), $"输出异常: {session.Output}");
            }
            finally { RoslynScriptRuntime.Shared.ReleaseSession(session); }
        });

        runner.Register(cat, "引用解析缓存命中同一实例", ctx =>
        {
            var path = typeof(object).Assembly.Location;
            var resolver = new RoslynScriptReferenceResolver();
            var first = resolver.LoadMetadataReferences(new[] { new ScriptReference(path, "core.dll"), new ScriptReference(path.ToUpperInvariant(), "core.dll") });
            var second = resolver.LoadMetadataReferences(new[] { new ScriptReference(path, "core.dll") });
            ctx.Assert(first.Count == 1, $"大小写重复路径应去重，实际 {first.Count}");
            ctx.Assert(ReferenceEquals(first[0], second[0]), "同一文件应命中缓存返回同一实例");
        });

        runner.Register(cat, "镜像清单改名后仍按标识回填", ctx =>
        {
            var dir = Path.Combine(Path.GetTempPath(), $"p5-manifest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var manifest = new ScriptFileManifest();
                manifest.Files.Add(new ScriptFileManifestEntry
                {
                    FileName = "01_alpha.cs",
                    ScriptId = Guid.NewGuid(),
                    ContentHash = ContentHash.Sha256Short("Console.WriteLine(1);")
                });
                manifest.Save(dir);
                var loaded = ScriptFileManifest.Load(dir);
                ctx.Assert(loaded != null && loaded.Files.Count == 1, "清单应写读往返");
                ctx.Assert(loaded!.Files[0].ScriptId == manifest.Files[0].ScriptId, "ScriptId 应稳定");
                var other = ContentHash.Sha256Short("Console.WriteLine(2);");
                ctx.Assert(!string.Equals(loaded.Files[0].ContentHash, other, StringComparison.Ordinal), "不同内容哈希应不同");
            }
            finally { Directory.Delete(dir, true); }
        });

        runner.Register(cat, "镜像清单外新增文件视为外部更新", async ctx =>
        {
            var dir = Path.Combine(Path.GetTempPath(), $"p5-newer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            var taskPath = Path.Combine(dir, "job.taskproj");
            try
            {
                var manager = new ScriptManagerService(new FileService(), new ScriptEditorService(), RoslynScriptRuntime.Shared);
                var item = manager.AddScriptItem("alpha");
                item.Code = "Console.WriteLine(1);";
                await manager.SaveToPathAsync(taskPath);
                ctx.Assert(!manager.FolderHasNewerSources(taskPath), "刚导出后应无外部更新");
                await File.WriteAllTextAsync(Path.Combine(dir, "02_beta.cs"), "Console.WriteLine(2);");
                ctx.Assert(manager.FolderHasNewerSources(taskPath), "清单外新增文件应视为外部更新");
                await File.WriteAllTextAsync(Path.Combine(dir, "01_alpha.cs"), "Console.WriteLine(9);");
                ctx.Assert(manager.FolderHasNewerSources(taskPath), "清单内文件改动应视为外部更新");
            }
            finally { Directory.Delete(dir, true); }
        });
    }
}
