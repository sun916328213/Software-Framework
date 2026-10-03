// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TestModulePaths。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.IO。导入不代表所有类型都会在运行时加载。
using System.IO;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// TestModulePaths 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
internal static class TestModulePaths
{
    public static string ModuleDirectory
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "FlowEditor.sln"))) return directory.FullName;
            throw new DirectoryNotFoundException("Cannot locate the FlowEditor module.");
        }
    }
}
