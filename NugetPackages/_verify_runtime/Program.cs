using ActUtlType64Lib;
using ActProgType64Lib;
using ActSupportMsg64Lib;
using ActUtlTypeLib;
using ActProgTypeLib;
using ActSupportMsgLib;

// 仅触发编译引用，验证 6 个 RCW 命名空间的类型可被消费方解析。
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
