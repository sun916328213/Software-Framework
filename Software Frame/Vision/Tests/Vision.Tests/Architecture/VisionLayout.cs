namespace Vision.Tests.Architecture;

/// <summary>当前 Vision 工程的物理布局；退役项目不保留空工程或别名登记。</summary>
internal static class VisionLayout
{
    internal static readonly (string Name, string Directory)[] Projects =
    [
        ("Vision.FlowNode", "Vision.FlowNode"),
        ("Vision.Core", "Vision.Core"),
        ("Vision.Tools", "Tools/Runtime"),
        ("Vision.Tools.Halcon", "Tools/Halcon"),
        ("Vision.Tools.OpenCv", "Tools/OpenCv"),
        ("Vision.AI.Halcon", "AI/Halcon"),
        ("Vision.Workbench", "Vision.Workbench"),
        ("Vision.Controls", "Vision.Controls"),
        ("Vision.Tests", "Tests/Vision.Tests"),
        ("Vision.Workbench.Tests", "Tests/Vision.Workbench.Tests")
    ];

    internal static string[] ProjectNames => Projects.Select(entry => entry.Name).ToArray();

    internal static string DirectoryOf(string projectName)
    {
        foreach (var entry in Projects)
        {
            if (string.Equals(entry.Name, projectName, StringComparison.Ordinal))
            {
                return entry.Directory;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(projectName), projectName, "未登记的 Vision 工程。");
    }

    internal static string ProjectDirectory(string visionRoot, string projectName) =>
        Path.Combine(visionRoot, DirectoryOf(projectName));

    internal static string ProjectFile(string visionRoot, string projectName) =>
        Path.Combine(ProjectDirectory(visionRoot, projectName), projectName + ".csproj");
}
