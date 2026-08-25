using System.Windows.Controls;

namespace WPFCustomControls.PropertyGrid.Views;

/// <summary>
/// 属性面板宿主视图。
/// 所有构建与编排逻辑均位于 <see cref="ViewModels.PropertyGridHostViewModel"/>，
/// 本视图只负责数据绑定渲染。
/// </summary>
public partial class PropertyGridHost : UserControl
{
    public PropertyGridHost()
    {
        InitializeComponent();
    }
}
