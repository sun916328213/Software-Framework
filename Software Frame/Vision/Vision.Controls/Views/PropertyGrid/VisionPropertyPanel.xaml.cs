using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Vision.Core;
using ValidationResult = Vision.Core.Results.ValidationResult;
using Vision.Controls.Presentation;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace Vision.Controls.View;

/// <summary>Vision 独立属性视图：共用 PropertyGrid.Core，布局/样式在 XAML，专用编辑器保持原 DTO 绑定。</summary>
public partial class VisionPropertyPanel : ScrollViewer
{
    private readonly VisionPropertyGridBuilder _builder;
    public PropertyGridHostViewModel Model { get; }
    public event EventHandler ParametersChanged;
    public static readonly DependencyProperty ParameterValidationProperty = DependencyProperty.Register(
        nameof(ParameterValidation), typeof(ValidationResult), typeof(VisionPropertyPanel),
        new PropertyMetadata(null, (sender, _) => ((VisionPropertyPanel)sender).UpdateValidationRows()));
    /// <summary>借用原工具的校验结果，不执行校验或保存第二份规则；无Member的问题仍由顶部摘要呈现。</summary>
    public ValidationResult ParameterValidation { get => (ValidationResult)GetValue(ParameterValidationProperty); set => SetValue(ParameterValidationProperty, value); }
    private void UpdateValidationRows()
    {
        foreach (var row in Model.Groups.SelectMany(group => group.AllItems).OfType<VisionPropertyItem>())
            row.SetValidation(ParameterValidation);
    }
    public bool AttachToolPresentation { get; set; } = true;
    public static readonly DependencyProperty TargetObjectProperty = DependencyProperty.Register(
        nameof(TargetObject), typeof(object), typeof(VisionPropertyPanel), new PropertyMetadata(null, TargetChanged));
    public object TargetObject { get => GetValue(TargetObjectProperty); set => SetValue(TargetObjectProperty, value); }
    public static readonly DependencyProperty AutoGeneratePropertiesProperty = DependencyProperty.Register(
        nameof(AutoGenerateProperties), typeof(bool), typeof(VisionPropertyPanel), new PropertyMetadata(false, OptionsChanged));
    public bool AutoGenerateProperties { get => (bool)GetValue(AutoGeneratePropertiesProperty); set => SetValue(AutoGeneratePropertiesProperty, value); }
    public static readonly DependencyProperty PropertyNamesProperty = DependencyProperty.Register(
        nameof(PropertyNames), typeof(string), typeof(VisionPropertyPanel), new PropertyMetadata(null, OptionsChanged));
    public string PropertyNames { get => (string)GetValue(PropertyNamesProperty); set => SetValue(PropertyNamesProperty, value); }
    public static readonly DependencyProperty PropertyAreaProperty = DependencyProperty.Register(
        nameof(PropertyArea), typeof(ToolPropertyArea?), typeof(VisionPropertyPanel), new PropertyMetadata(null, OptionsChanged));
    public ToolPropertyArea? PropertyArea { get => (ToolPropertyArea?)GetValue(PropertyAreaProperty); set => SetValue(PropertyAreaProperty, value); }
    public static readonly DependencyProperty ExpandGroupsProperty = DependencyProperty.Register(
        nameof(ExpandGroups), typeof(bool), typeof(VisionPropertyPanel), new PropertyMetadata(false));
    public bool ExpandGroups { get => (bool)GetValue(ExpandGroupsProperty); set => SetValue(ExpandGroupsProperty, value); }

    public VisionPropertyPanel()
    {
        _builder = new(row => CreateEditor(row.Property, row.Metadata, row.Target));
        Model = new(_builder, new NoPortDialogs());
        InitializeComponent();
        AddHandler(Binding.SourceUpdatedEvent, new EventHandler<DataTransferEventArgs>((_, e) =>
        {
            // 搜索/模板首次生成时的绑定初始化不是用户编辑，不能误清结果或标记重训。
            if (e.TargetObject is FrameworkElement { IsLoaded: true }) ParametersChanged?.Invoke(this, EventArgs.Empty);
        }));
        Loaded += (_, _) => { if (!ReferenceEquals(Model.TargetObject, TargetObject)) Refresh(); };
        // 仅释放属性行的订阅；TargetObject 是借用的 DTO，不销毁它。
        Unloaded += (_, _) => Model.TargetObject = null;
    }
    private static void TargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var panel = (VisionPropertyPanel)sender;
        if (panel.AttachToolPresentation && e.NewValue is Vision.Core.Tools.Tool tool)
            tool.AttachPresentation(new WpfToolPresentation());
        panel.Refresh();
    }
    private static void OptionsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((VisionPropertyPanel)sender).Refresh();
    public void Refresh()
    {
        _builder.AutoGenerateProperties = AutoGenerateProperties;
        _builder.PropertyNames = PropertyNames;
        _builder.PropertyArea = PropertyArea;
        Model.TargetObject = null;
        Model.TargetObject = TargetObject;
        UpdateValidationRows();
    }
    internal static void ApplyEditorStyle(FrameworkElement editor)
    {
        var key = editor switch
        {
            NumericUpDown => "Integer",
            DecimalUpDown => "Decimal",
            TextBox => "Text",
            VisionMultiSelectComboBox => "MultiSelect",
            ComboBox => "Select",
            CheckBox => "Check",
            Button => "Button",
            _ => null
        };
        if (key != null) editor.SetResourceReference(StyleProperty, "Vision.PropertyPanel." + key);
    }
    private void LabelMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TextBlock { ToolTip: ToolTip tip }) tip.IsOpen = true;
    }
    private void LabelMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is TextBlock { ToolTip: ToolTip tip }) tip.IsOpen = false;
    }
    private sealed class NoPortDialogs : IPortDialogService
    {
        public PortDialogResult ShowAddPortDialog(PortDialogRequest request) => null;
        public PortDialogResult ShowModifyBindingDialog(PortDialogRequest request) => null;
    }
    private void NotifyRowsChanged(object target)
    {
        if (ReferenceEquals(TargetObject, target)) ParametersChanged?.Invoke(this, EventArgs.Empty);
    }

    private FrameworkElement CreateEditor(PropertyInfo prop, ToolPropertyAttribute attr, object target)
    {
        FrameworkElement editor = null;

        bool propIsSetPublic = PropertyEditorBindingHelper.IsEditableProperty(prop);

        bool contentEditable = attr.IsContentEditable && prop.GetValue(target) is System.Collections.IList &&
            attr.EditorType is E_PropertyEditorType.MultiSelect or E_PropertyEditorType.DataGridList or E_PropertyEditorType.DataClassList;
        if (!propIsSetPublic && !contentEditable && attr.EditorType != E_PropertyEditorType.Button)
            return CreateReadOnlyEditor(prop, target);

        if (prop.PropertyType == typeof(string) && !string.IsNullOrWhiteSpace(attr.FileDialogFilter))
        {
            var picker = new WPFCustomControls.PropertyGrid.Views.FilePathEditor { Filter = attr.FileDialogFilter };
            picker.SetBinding(WPFCustomControls.PropertyGrid.Views.FilePathEditor.TextProperty, new Binding(prop.Name)
            {
                Source = target, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                NotifyOnSourceUpdated = true, ValidatesOnExceptions = true
            });
            return picker;
        }

        switch (attr.EditorType)
        {
            case E_PropertyEditorType.DoubleList:
                editor = prop.PropertyType == typeof(List<double>)
                    ? DoubleListPropertyEditor.Create(prop.Name, target) : CreateReadOnlyEditor(prop, target);
                break;
            case E_PropertyEditorType.Point:
                editor = prop.PropertyType == typeof(Vision.Core.Geometry.Point2D)
                    ? PointPropertyEditor.Create(prop, target) : CreateReadOnlyEditor(prop, target);
                break;
            case E_PropertyEditorType.Int:
                var nmb = new NumericUpDown
                {

                    ValueStep = Convert.ToInt32(attr.Step == 0 ? 1 : attr.Step),
                    Maximum = Convert.ToInt32(double.IsNaN(attr.Max) ? int.MaxValue : attr.Max),
                    Minimum = Convert.ToInt32(double.IsNaN(attr.Min) ? int.MinValue : attr.Min) };

                  BindingOperations.SetBinding(nmb, NumericUpDown.ValueProperty,
                    new Binding(prop.Name)
                    {
                        NotifyOnSourceUpdated = true,
                        ValidatesOnExceptions = true,
                        Source = target,
                        Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });
                if (!propIsSetPublic)
                {
                    nmb.IsEnabled = false;
                }
                editor = nmb;
                break;
            case E_PropertyEditorType.Double:
                var dm = new DecimalUpDown {

                    ValueStep = Convert.ToDecimal(attr.Step == 0 ? 0.001 : attr.Step),
                    Maximum = double.IsNaN(attr.Max) ? decimal.MaxValue : Convert.ToDecimal(attr.Max),
                    Minimum = double.IsNaN(attr.Min) ? decimal.MinValue : Convert.ToDecimal(attr.Min) };
                BindingOperations.SetBinding(dm, DecimalUpDown.ValueProperty,
                    new Binding(prop.Name)
                    {
                        NotifyOnSourceUpdated = true,
                        ValidatesOnExceptions = true,
                        Source = target,
                        Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });
                if (!propIsSetPublic)
                {
                    dm.IsEnabled = false;
                }
                editor = dm;
                break;
            case E_PropertyEditorType.Text:
                var tb = new TextBox();
                Binding bd = new Binding(prop.Name)
                {
                    NotifyOnSourceUpdated = true,
                    ValidatesOnExceptions = true,
                    Source = target,
                    Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                // 显式空值映射让 nullable 坐标清空时回写 null，而非保留上一次有效数值。
                if (Nullable.GetUnderlyingType(prop.PropertyType) != null) bd.TargetNullValue = string.Empty;
                BindingOperations.SetBinding(tb, TextBox.TextProperty, bd);

                BindingOperations.SetBinding(tb, TextBox.ToolTipProperty,
                    new Binding(prop.Name) { Source = target, Mode = BindingMode.OneWay });

                tb.ToolTipOpening += (s2, e2) =>
                {
                    var formatted = new FormattedText(
                        tb.Text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                        tb.FontSize,
                        Brushes.Black,
                        new NumberSubstitution(),
                        1);

                    if (formatted.Width > tb.ActualWidth)
                    {
                        tb.ToolTip = tb.Text; // 超出才显示
                    }
                    else
                    {
                        tb.ToolTip = null; // 没超出就不显示
                        e2.Handled = true;
                    }
                };
                if (!propIsSetPublic)
                {
                    tb.IsReadOnly = true;
                }
                editor = tb;

                break;
            case E_PropertyEditorType.SingleSelect:
                var cb = new ComboBox();
                if (prop.PropertyType.IsEnum)
                {
                    cb.ItemsSource = Enum.GetValues(prop.PropertyType);
                }
                else if (attr.Options != null && attr.Options.Length != 0)
                {
                    cb.ItemsSource = attr.Options;
                }
                else cb.ItemsSource = ResolveItemsSource(attr, target);
                BindingOperations.SetBinding(cb, ComboBox.SelectedItemProperty,
                    new Binding(prop.Name) {
                        NotifyOnSourceUpdated = true,
                        Source = target,
                        Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay,
                    });
                if (!propIsSetPublic)
                {
                    cb.IsEnabled = false;
                }
                editor = cb;
                break;
            case E_PropertyEditorType.MultiSelect:
                var mcb = new Vision.Controls.VisionMultiSelectComboBox();

                Type elementType = null;

                if (prop.PropertyType.IsArray)
                {
                    elementType = prop.PropertyType.GetElementType();
                }
                else if (prop.PropertyType.IsGenericType)
                {
                    elementType = prop.PropertyType.GetGenericArguments()[0];
                }

                if (elementType != null && elementType.IsEnum)
                {
                    mcb.ItemsSource = Enum.GetValues(elementType);
                }
                else if (attr.Options != null && attr.Options.Length != 0)
                {
                    mcb.ItemsSource = attr.Options;
                }
                else mcb.ItemsSource = ResolveItemsSource(attr, target);

                // 绑定 SelectedItems 到对象的属性
                BindingOperations.SetBinding(mcb, Vision.Controls.VisionMultiSelectComboBox.SelectedItemsProperty,
                    new Binding(prop.Name)
                    {
                        Source = target,
                        Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay
                    });
                mcb.SelectionContentChanged += (s, e) => ParametersChanged?.Invoke(this, EventArgs.Empty);
                editor = mcb;
                editor.IsEnabled = (propIsSetPublic || contentEditable) &&
                    prop.GetValue(target) is System.Collections.IList selection && !selection.IsReadOnly && !selection.IsFixedSize;
                break;
            case E_PropertyEditorType.Checkbox:
                var chk = new CheckBox();
                BindingOperations.SetBinding(chk, CheckBox.IsCheckedProperty,
                    new Binding(prop.Name) { Source = target, NotifyOnSourceUpdated = true, Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay });

                editor = chk;
                editor.IsEnabled = propIsSetPublic;
                break;
            case E_PropertyEditorType.Button:
                var btn = new Button { Content = attr.DisplayName};
                if (prop.GetValue(target) is ButtonAction bta)
                {
                    if (bta.OnBtClick != null)
                    {
                        RoutedEventHandler handler = new RoutedEventHandler((s, e) => bta.OnBtClick.Invoke(s, e));
                        //btn.Click += handler;
                        btn.Loaded += (s, e) =>
                        {
                            btn.Click -= handler;
                            btn.Click += handler;
                        };
                        btn.Unloaded += (s, e) => { btn.Click -= handler; };
                    }
                }
                editor = btn; break;
            case E_PropertyEditorType.DataGridList:
            {
                var table = new VisionPropertyTableEditor();
                table.ConfigureList(prop, target, propIsSetPublic || contentEditable,
                    propIsSetPublic && prop.GetValue(target) is System.Collections.IList list &&
                        !list.IsReadOnly && !list.IsFixedSize,
                    propIsSetPublic, () => NotifyRowsChanged(target));
                editor = table; break;
            }
            case E_PropertyEditorType.DataClassList:
            {
                var table = new VisionPropertyTableEditor();
                table.ConfigureClass(prop, attr, target, propIsSetPublic || contentEditable,
                    () => NotifyRowsChanged(target));
                editor = table; break;
            }
        }
        if (editor is not DataGrid) ApplyEditorStyle(editor);
        return editor;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 IsEditableProperty。
    /// 正文中的关键协作调用包括 IsEditableProperty；具体条件和先后次序见方法体。
    /// </summary>
    private static System.Collections.IEnumerable ResolveItemsSource(ToolPropertyAttribute attr, object target)
    {
        if (string.IsNullOrWhiteSpace(attr.ListItemsSourceName)) return null;
        var type = attr.ListItemsSourceType ?? target.GetType();
        var flags = BindingFlags.Public | (attr.ListItemsSourceType == null ? BindingFlags.Instance : BindingFlags.Static);
        return type.GetProperty(attr.ListItemsSourceName, flags)?.GetValue(attr.ListItemsSourceType == null ? target : null)
            as System.Collections.IEnumerable;
    }

    /// <summary>
    /// 创建当前实现提供的对象或结果；对应入口为 CreateReadOnlyEditor。
    /// 正文中的关键协作调用包括 SetBinding；具体条件和先后次序见方法体。
    /// </summary>
    private FrameworkElement CreateReadOnlyEditor(PropertyInfo prop, object target)
    {
        var textBlock = new TextBlock();
        textBlock.SetResourceReference(StyleProperty, "Vision.PropertyPanel.ReadOnly");

        BindingOperations.SetBinding(textBlock, TextBlock.TextProperty,
            new Binding(prop.Name)
            {
                Source = target,
                Mode = BindingMode.OneWay,
                TargetNullValue = string.Empty
            });

        BindingOperations.SetBinding(textBlock, TextBlock.ToolTipProperty,
            new Binding(prop.Name)
            {
                Source = target,
                Mode = BindingMode.OneWay
            });

        return textBlock;
    }
}
