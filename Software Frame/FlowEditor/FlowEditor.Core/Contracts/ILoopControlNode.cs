// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。
// 本文件定义：ILoopControlNode。成员说明可结合下方具体实现阅读。
namespace FlowEditor.Core;

/// <summary>
/// 循环控制节点接口（Blueprint 风格）。
/// <para>
/// 实现后由引擎在图编译期自动补全回边：用户只需把 Body 端口连到循环体第一个节点，
/// 循环体末端<b>无需手动接回循环节点</b>，Runtime 的循环图规范化器会为循环体末端
/// 自动合成一条回边（末端指向循环体的出边为空、或只指向循环体外部时判定为末端）。
/// </para>
/// <para>
/// 若用户已显式画了回边（老图），自动合成会跳过该末端，行为与旧版完全一致。
/// </para>
/// </summary>
public interface ILoopControlNode
{
    /// <summary>循环体输出端口 ID 集合。从这些端口出发可达的节点构成循环体。</summary>
    IReadOnlyCollection<string> BodyPortIds { get; }

    /// <summary>回边回到的输入端口 ID（通常为内置 Next_IN）。每次取用动态计算，避免反序列化后 ID 失效。</summary>
    string LoopBackPortId { get; }
}
