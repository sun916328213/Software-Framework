using Vision.Controls.Models;
namespace Vision.Controls.Catalog;

/// <summary>既有公共几何数学的宿主注册，不创建另一个Tools项目或执行器。</summary>
public static class CommonToolCatalog
{
    public static IReadOnlyList<ToolEntry> Entries { get; } = [ToolRegistration.CommonGeometry().Entry, ToolRegistration.CommonTolerance().Entry];
}
