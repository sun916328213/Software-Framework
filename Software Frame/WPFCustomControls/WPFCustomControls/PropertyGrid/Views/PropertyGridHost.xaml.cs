// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。
// 本文件定义：PropertyGridHost。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System.Windows.Controls。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System.Windows.Controls;

namespace WPFCustomControls.PropertyGrid.Views;

/// <summary>
/// 属性面板宿主视图。
/// 所有构建与编排逻辑均位于 <see cref="ViewModels.PropertyGridHostViewModel"/>，
/// 本视图只负责数据绑定渲染。
/// </summary>
public partial class PropertyGridHost : UserControl
{
    /// <summary>
    /// 初始化 PropertyGridHost 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
    /// 正文中的关键协作调用包括 InitializeComponent；具体条件和先后次序见方法体。
    /// </summary>
    public PropertyGridHost()
    {
        InitializeComponent();
    }
}
