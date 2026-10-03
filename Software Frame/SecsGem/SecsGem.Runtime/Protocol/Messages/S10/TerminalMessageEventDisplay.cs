// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：TerminalMessageEventDisplay。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Runtime.Protocol、SecsGem.Core、SecsGem.Runtime。导入不代表所有类型都会在运行时加载。
using SecsGem.Runtime.Protocol;
using SecsGem.Core;
using SecsGem.Runtime;

namespace SecsGem.Runtime.Protocol.Messages.S10;

/// <summary>Forwards terminal text to the runtime without choosing a UI technology.</summary>
/// <remarks>
/// TerminalMessageEventDisplay 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
/// 继承或实现的契约：ITerminalDisplay。具体约束按这些契约及各成员实现解释。
/// </remarks>
internal sealed class TerminalMessageEventDisplay(Action<string, string> callback) : ITerminalDisplay
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Show。
    /// 正文中的关键协作调用包括 callback；具体条件和先后次序见方法体。
    /// </summary>
    public void Show(string title, string message) => callback(title, message);
}
