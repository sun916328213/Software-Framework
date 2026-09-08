namespace WPFCustomControls.PropertyGrid.Models;

/// <summary>
/// 标记需要在属性面板中显示的节点属性。
/// 仿 Vision 项目中的 ToolPropertyAttribute，为节点模型的属性提供声明式标注。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NodePropertyAttribute : Attribute
{
    /// <summary>属性在面板中的显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>属性所属分组（如 "1.基础信息"、"2.布局"）。为空则归入"通用"分组。</summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>属性描述/提示文本。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>排序序号（越小越靠前）。默认 0。</summary>
    public int Order { get; set; } = 0;

    /// <summary>是否在面板中显示此属性。默认 true。</summary>
    public bool IsDisplay { get; set; } = true;

    /// <summary>
    /// 是否为只读属性。
    /// 0 = 自动判断（根据 setter 是否公开），1 = 强制只读，-1 = 强制可编辑。
    /// </summary>
    public int ReadOnly { get; set; } = 0;

    /// <summary>最小值（数值类型时生效）。</summary>
    public double Min { get; set; } = double.NaN;

    /// <summary>最大值（数值类型时生效）。</summary>
    public double Max { get; set; } = double.NaN;

    /// <summary>数值步长。未设置时整数默认 1，小数默认 0.01。</summary>
    public double Step { get; set; } = 0;

    /// <summary>是否作为节点的输入端口（可被其他节点连线赋值）。编译期默认值。</summary>
    public bool IsInput { get; set; } = false;

    /// <summary>是否作为节点的输出端口（可连线到其他节点的输入）。编译期默认值。</summary>
    public bool IsOutput { get; set; } = false;

    /// <summary>是否在属性面板中显示内联端口开关（☑入 ☑出），运行时覆盖 IsInput/IsOutput。</summary>
    public bool PortConfigurable { get; set; } = true;

    /// <summary>
    /// 下拉选项数据源成员名：目标对象上返回 string 集合（如 List&lt;string&gt;）的属性名。
    /// 声明后，该非枚举属性渲染为 ComboBox，选项动态取自该集合。
    /// 每个下拉属性各自声明一个数据源成员，互不干扰（多下拉无需 switch 分发）。
    /// </summary>
    public string ItemsSource { get; set; } = string.Empty;
}