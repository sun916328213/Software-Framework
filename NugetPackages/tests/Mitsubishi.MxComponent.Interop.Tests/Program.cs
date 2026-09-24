using ActUtlType64Lib;
using ActProgType64Lib;
using ActSupportMsg64Lib;
using ActUtlTypeLib;
using ActProgTypeLib;
using ActSupportMsgLib;

// 仅触发编译引用，验证 6 个 RCW 命名空间的类型可被消费方解析。
// 不创建 COM 对象，不依赖本机 MX Component 安装。
var types = new Type[]
{
    typeof(ActUtlType64Class),
    typeof(ActProgType64Class),
    typeof(ActSupportMsg64Class),
    typeof(ActUtlTypeClass),
    typeof(ActProgTypeClass),
    typeof(ActSupportMsgClass),
};
Console.WriteLine(string.Join(", ", types.Select(t => t.Name)));

// 输出布局断言：6 个 RCW 必须作为 NuGet 运行时资产出现在输出目录。
// 缺失即验收失败（run consumer 可失败）。
string[] expected =
[
    "Interop.ActUtlType64Lib.dll",
    "Interop.ActUtlTypeLib.dll",
    "Interop.ActProgType64Lib.dll",
    "Interop.ActProgTypeLib.dll",
    "Interop.ActSupportMsg64Lib.dll",
    "Interop.ActSupportMsgLib.dll",
];

foreach (var file in expected)
{
    if (!File.Exists(Path.Combine(AppContext.BaseDirectory, file)))
        throw new FileNotFoundException($"NuGet runtime asset missing: {file}");
}

Console.WriteLine("runtime assets ok: 6/6");
