// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：WpfToolPresentation。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections、System.Windows、System.Windows.Data、Vision.Core.Presentation。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections;
using System.Windows;
using System.Windows.Data;
using Vision.Core.Presentation;

namespace Vision.Controls.Presentation;

/// <summary>
/// WpfToolPresentation 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// 继承或实现的契约：IToolPresentation。具体约束按这些契约及各成员实现解释。
/// </summary>
public sealed class WpfToolPresentation : IToolPresentation
{
    private readonly Action<string> _showMessage;

    /// <summary>
    /// 初始化 WpfToolPresentation 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 Show；具体条件和先后次序见方法体。
    /// </summary>
    public WpfToolPresentation(Action<string> showMessage = null) =>
        _showMessage = showMessage ?? (message => MessageBox.Show(message));

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ShowMessage。
    /// 正文中的关键协作调用包括 _showMessage；具体条件和先后次序见方法体。
    /// </summary>
    public void ShowMessage(string message) => _showMessage(message);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Enable。
    /// 正文中的关键协作调用包括 EnableCollectionSynchronization；具体条件和先后次序见方法体。
    /// </summary>
    public void Enable(IEnumerable collection, object synchronizationLock) =>
        BindingOperations.EnableCollectionSynchronization(collection, synchronizationLock);
    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Disable。
    /// 正文中的关键协作调用包括 DisableCollectionSynchronization；具体条件和先后次序见方法体。
    /// </summary>
    public void Disable(IEnumerable collection) => BindingOperations.DisableCollectionSynchronization(collection);
}
