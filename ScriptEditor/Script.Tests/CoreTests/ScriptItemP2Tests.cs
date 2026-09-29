using Extension.Core.Conversion;
using Script.Tests.Core;
using ScriptEditor.Models;
using ScriptEditor.Services;
using System.IO;
using System.Windows.Threading;

namespace Script.Tests.CoreTests;

public static class ScriptItemP2Tests
{
    public static void Register(TestRunner runner)
    {
        runner.Register("P2 数据模型", "引用描述保留路径与程序集名", ctx =>
        {
            var item = new ScriptItem();
            item.SetReferences(new[] { @"C:\scripts\assemblies\Driver.dll" });

            ctx.Assert(item.ScriptReferences.Count == 1, "应保留一条引用描述");
            ctx.Assert(item.ScriptReferences[0].Path == @"C:\scripts\assemblies\Driver.dll", "引用路径应原样保留");
            ctx.Assert(item.ScriptReferences[0].Name == "Driver.dll", "引用名称应为程序集文件名");
        });

        runner.Register("P2 数据模型", "项目脚本始终具有有效标识", ctx =>
        {
            var script = new ScriptProjectItem { ScriptId = Guid.Empty };
            ctx.Assert(script.ScriptId != Guid.Empty, "空标识必须生成新的永久标识");
        });

        runner.Register("P2 数据模型", "旧任务文件生成并保存脚本标识", ctx =>
        {
            const string legacyJson = """
                {"Name":"legacy","References":[],"Scripts":[
                  {"Title":"one","Code":"Console.WriteLine(1);","References":["missing-driver.dll"]},
                  {"Title":"two","Code":"Console.WriteLine(2);","References":[]}
                ]}
                """;
            var project = legacyJson.FromJson<TaskProjectModel>();
            ctx.Assert(project?.Scripts.Count == 2, "旧任务文件应加载两个脚本");
            if (project?.Scripts.Count != 2) return;

            var firstId = project.Scripts[0].ScriptId;
            var secondId = project.Scripts[1].ScriptId;
            ctx.Assert(firstId != Guid.Empty && secondId != Guid.Empty && firstId != secondId,
                "旧脚本应取得各自独立的非空标识");
            ctx.Assert(project.Scripts[0].References.Single() == "missing-driver.dll",
                "缺失 DLL 路径应保留在数据模型中供后续迁移或修复");

            var reloaded = project.ToJson().FromJson<TaskProjectModel>();
            ctx.Assert(reloaded?.Scripts[0].ScriptId == firstId && reloaded?.Scripts[1].ScriptId == secondId,
                "保存并重新加载后 ScriptId 应保持稳定");
        });

        runner.Register("P2 数据模型", "脚本断点独立序列化", ctx =>
        {
            var item = new ScriptItem { Title = "debug", Code = "Console.WriteLine(1);" };
            item.Breakpoints.Add(4);
            item.Breakpoints.Add(9);

            var copy = item.ToJson().FromJson<ScriptItem>();
            ctx.Assert(copy?.Breakpoints.SequenceEqual(new[] { 4, 9 }) == true,
                "断点行号应随脚本数据保存并恢复");
        });

        runner.Register("P2 数据模型", "旧执行调用由运行服务完成", async ctx =>
        {
            var item = new ScriptItem { Code = "Console.WriteLine(\"p2-item-run\");" };
            try
            {
                var succeeded = await item.ExecuteAsync();
                await Dispatcher.Yield(DispatcherPriority.Background);
                ctx.Assert(succeeded, $"兼容执行调用失败: {item.StatusText}");
                ctx.Assert(item.OutputText.Contains("p2-item-run", StringComparison.Ordinal),
                    $"执行输出未同步到绑定状态: {item.OutputText}");
            }
            finally { item.DisposeExecution(); }
        });

        runner.Register("P2 数据模型", "管理器加载旧任务后可执行", async ctx =>
        {
            const string legacyJson = """
                {"Name":"legacy-manager","References":[],"Scripts":[
                  {"Title":"loaded","Code":"Console.WriteLine(\"loaded-p2\");","References":[]}
                ]}
                """;
            var path = Path.Combine(Path.GetTempPath(), $"scripteditor-p2-{Guid.NewGuid():N}.taskproj");
            try
            {
                await File.WriteAllTextAsync(path, legacyJson);
                var manager = new ScriptManagerService(new FileService(), new ScriptEditorService());
                await manager.LoadTaskFromPathAsync(path);
                ctx.Assert(manager.Items.Count == 1, $"旧任务应加载一项，实际 {manager.Items.Count}");
                if (manager.Items.Count != 1) return;
                ctx.Assert(manager.Items[0].ScriptId != Guid.Empty, "加载后应获得永久 ScriptId");
                await manager.RunAllAsync();
                await Dispatcher.Yield(DispatcherPriority.Background);
                ctx.Assert(manager.Items[0].OutputText.Contains("loaded-p2", StringComparison.Ordinal),
                    $"加载后执行输出不正确: {manager.Items[0].OutputText}");
                manager.StopAll();
            }
            finally { File.Delete(path); }
        });

        runner.Register("P2 数据模型", "管理器加载保留脚本标识与断点", async ctx =>
        {
            var original = new ScriptItem { Title = "persisted", Code = "Console.WriteLine(1);" };
            original.Breakpoints.Add(5);
            var project = new TaskProjectModel { Scripts = new List<ScriptItem> { original } };
            var path = Path.Combine(Path.GetTempPath(), $"scripteditor-p2-{Guid.NewGuid():N}.taskproj");
            try
            {
                await File.WriteAllTextAsync(path, project.ToJson());
                var manager = new ScriptManagerService(new FileService(), new ScriptEditorService());
                await manager.LoadTaskFromPathAsync(path);
                ctx.Assert(manager.Items.Count == 1, "保存的任务应加载一项");
                if (manager.Items.Count != 1) return;
                ctx.Assert(manager.Items[0].ScriptId == original.ScriptId, "ScriptId 应跨管理器加载保持不变");
                ctx.Assert(manager.Items[0].Breakpoints.SequenceEqual(new[] { 5 }), "断点应跨管理器加载保持不变");
            }
            finally { File.Delete(path); }
        });

        runner.Register("P2 数据模型", "复制脚本保留内容但生成新标识", ctx =>
        {
            var manager = new ScriptManagerService(new FileService(), new ScriptEditorService());
            var original = manager.AddScriptItem("source");
            original.Code = "Console.WriteLine(42);";
            original.SetReferences(new[] { "missing-driver.dll" });
            original.Breakpoints.Add(3);
            manager.Model.SelectedItem = original;

            manager.CopyScript();
            ctx.Assert(manager.Items.Count == 2, "复制后应有两个脚本");
            if (manager.Items.Count != 2) return;
            var copy = manager.Items[1];
            ctx.Assert(copy.ScriptId != original.ScriptId, "副本必须取得独立 ScriptId");
            ctx.Assert(copy.Code == original.Code, "副本应保留代码");
            ctx.Assert(copy.References.SequenceEqual(original.References), "副本应保留 DLL 路径数据");
            ctx.Assert(copy.Breakpoints.SequenceEqual(original.Breakpoints), "副本应保留断点行号");
        });
    }
}
