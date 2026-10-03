// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsOrderAdapter。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Patterns.Creation.Factory.Advanced、SecsGem.Core.Order、SecsGem.Runtime.Order。导入不代表所有类型都会在运行时加载。
using Extension.Patterns.Creation.Factory.Advanced;
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;

namespace SecsGem.Runtime.SemiTool.Tool;

/// <summary>Compatibility adapter for the old factory API.</summary>
/// <remarks>
/// SecsOrderAdapter 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：IAdvancedProduct。具体约束按这些契约及各成员实现解释。
/// </remarks>
public class SecsOrderAdapter : IAdvancedProduct
{
    private readonly SecsOrderBase _secsOrder;

    public string Id { get; }
    public string Name { get; }
    public string Type => "Secs命令";

    /// <summary>
    /// 初始化 SecsOrderAdapter 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 nameof、GetType；具体条件和先后次序见方法体。
    /// </summary>
    public SecsOrderAdapter(SecsOrderBase secsOrder, string productId)
    {
        _secsOrder = secsOrder ?? throw new ArgumentNullException(nameof(secsOrder));
        Id = productId ?? throw new ArgumentNullException(nameof(productId));
        Name = secsOrder.GetType().Name;
    }

    /// <summary>
    /// 执行当前入口的处理流程；对应入口为 Execute。
    /// 正文中的关键协作调用包括 WriteLine；具体条件和先后次序见方法体。
    /// </summary>
    public void Execute() => Console.WriteLine($"Secs命令 {Name} 已创建");

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetSecsOrder。
    /// </summary>
    public SecsOrderBase GetSecsOrder() => _secsOrder;

    /// <summary>
    /// 执行资源释放步骤；对应入口为 Dispose。
    /// </summary>
    public void Dispose() { }
}
