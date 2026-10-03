// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：ICollectionSynchronization、IToolPresentation。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections。导入不代表所有类型都会在运行时加载。
using System.Collections;

namespace Vision.Core.Presentation;

/// <summary>Optional host adapter; the model owns collection locking, the host owns binding.</summary>
/// <remarks>
/// ICollectionSynchronization 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </remarks>
public interface ICollectionSynchronization
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Enable。
    /// </summary>
    void Enable(IEnumerable collection, object synchronizationLock);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Disable。
    /// </summary>
    void Disable(IEnumerable collection);
}

/// <summary>
/// IToolPresentation 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：ICollectionSynchronization。具体约束按这些契约及各成员实现解释。
/// </summary>
public interface IToolPresentation : ICollectionSynchronization
{
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShowMessage。
    /// </summary>
    void ShowMessage(string message);
}
