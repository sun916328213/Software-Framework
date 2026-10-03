// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：PropertyGridThemeAssist、ChromeState。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Media、Xceed.Wpf.Toolkit.PropertyGrid、System.Runtime.CompilerServices。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xceed.Wpf.Toolkit.PropertyGrid;
using System.Runtime.CompilerServices;

namespace Vision.Controls.Presentation;

/// <summary>Adapts Xceed's default light chrome to the host's live Material Design theme.</summary>
/// <remarks>
/// PropertyGridThemeAssist 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
/// </remarks>
public static class PropertyGridThemeAssist
{
    private static readonly ConditionalWeakTable<PropertyGrid, ChromeState> States = new();
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PropertyGridThemeAssist), new PropertyMetadata(false, Changed));

    /// <summary>
    /// 获取当前实现提供的值或对象；对应入口为 GetIsEnabled。
    /// 正文中的关键协作调用包括 GetValue；具体条件和先后次序见方法体。
    /// </summary>
    public static bool GetIsEnabled(DependencyObject target) => (bool)target.GetValue(IsEnabledProperty);
    /// <summary>
    /// 设置当前实现管理的值或状态；对应入口为 SetIsEnabled。
    /// 正文中的关键协作调用包括 SetValue；具体条件和先后次序见方法体。
    /// </summary>
    public static void SetIsEnabled(DependencyObject target, bool value) => target.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Changed。
    /// 正文中的关键协作调用包括 TryGetValue、Remove、SetResourceReference、Add、SetCurrentValue；具体条件和先后次序见方法体。
    /// </summary>
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not PropertyGrid grid) return;
        if (States.TryGetValue(grid, out var previous))
        {
            grid.LayoutUpdated -= previous.Update;
            States.Remove(grid);
        }
        if (!(bool)args.NewValue) return;
        grid.SetResourceReference(Control.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        grid.SetResourceReference(Control.ForegroundProperty, "MaterialDesign.Brush.Foreground");
        var itemStyle = new Style(typeof(PropertyItem));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("MaterialDesign.Brush.Card.Background")));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("MaterialDesign.Brush.Foreground")));
        grid.SetCurrentValue(PropertyGrid.PropertyContainerStyleProperty, itemStyle);
        var state = new ChromeState(grid);
        States.Add(grid, state);
        grid.LayoutUpdated += state.Update;
    }

    /// <summary>
    /// ChromeState 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    private sealed class ChromeState(PropertyGrid grid)
    {
        private readonly ConditionalWeakTable<DependencyObject, object> _seen = new();
        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// 正文中的关键协作调用包括 Visit；具体条件和先后次序见方法体。
        /// </summary>
        public void Update(object sender, System.EventArgs args) => Visit(grid, false, false);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Visit。
        /// 正文中的关键协作调用包括 GetType、TryGetValue、Add、ApplyChrome、GetChildrenCount；具体条件和先后次序见方法体。
        /// </summary>
        private void Visit(DependencyObject element, bool inItem, bool inEditor)
        {
            inItem |= element is PropertyItem;
            inEditor |= element.GetType().Namespace == "Xceed.Wpf.Toolkit.PropertyGrid.Editors";
            if (!_seen.TryGetValue(element, out _))
            {
                _seen.Add(element, new object());
                if (!inItem || inEditor) ApplyChrome(element, inEditor);
            }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                Visit(VisualTreeHelper.GetChild(element, i), inItem, inEditor);
        }
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ApplyChrome。
    /// 正文中的关键协作调用包括 HasSurface、SetResourceReference；具体条件和先后次序见方法体。
    /// </summary>
    private static void ApplyChrome(DependencyObject element, bool inEditor)
    {
        // Theme each realized part once; leave the row selection/focus template intact.
        bool HasSurface(Brush brush) => brush is SolidColorBrush solid ? solid.Color.A > 0 : inEditor && brush is not null;
        if (element is Border border && HasSurface(border.Background))
            border.SetResourceReference(Border.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        if (element is Panel panel && panel.Background is SolidColorBrush panelBrush && panelBrush.Color.A > 0)
            panel.SetResourceReference(Panel.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        if (element is Control control)
        {
            control.SetResourceReference(Control.ForegroundProperty, "MaterialDesign.Brush.Foreground");
            if (HasSurface(control.Background))
                control.SetResourceReference(Control.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
        }
    }
}
