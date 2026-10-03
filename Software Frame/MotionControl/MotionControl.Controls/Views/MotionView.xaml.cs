// 【中文阅读说明】
// MotionControl 模块：运动契约、控制卡适配和验证界面；实际轴状态、命令结果及释放顺序遵守设备实现。
// 本文件定义：MotionView。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using System.Windows.Controls;

namespace MotionControl.Controls.Views;

/// <summary>可供业务宿主直接导航的完整运动页面。</summary>
public partial class MotionView : UserControl
{
    /// <summary>
    /// 初始化 MotionView 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public MotionView() => InitializeComponent();
}
