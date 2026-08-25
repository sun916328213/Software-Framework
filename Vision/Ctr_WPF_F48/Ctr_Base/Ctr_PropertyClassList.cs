
using DPLibrary_Base_F48;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Ctr_WPF_F48
{

    public sealed class PropMeta 
    {
        public PropMeta(PropertyInfo prop, ToolPropertyAttribute attr) 
        {
            Prop = prop;
            Attr = attr;
        }
        public PropertyInfo Prop { get; set; }
        public ToolPropertyAttribute Attr { get; set; }
    }

    public sealed class GroupedProps
    {
        public GroupedProps() { }
        public GroupedProps(string group, List<PropMeta> items)
        {
            Group = group;
            Items = items;
        }
        public string Group { get; set; }
        public List<PropMeta> Items { get; set; }
    }

    public class Ctr_PropertyClassList : ScrollViewer
    {
        private StackPanel mRoot;

        public static readonly DependencyProperty TargetObjectProperty =
            DependencyProperty.Register(nameof(TargetObject), typeof(object), typeof(Ctr_PropertyClassList),
                new PropertyMetadata(null, OnTargetObjectChanged));

        public object TargetObject
        {
            get => GetValue(TargetObjectProperty);
            set => SetValue(TargetObjectProperty, value);
        }

        public Ctr_PropertyClassList()
        {
            mRoot = new StackPanel();
            this.Content = mRoot;
        }

        private static void OnTargetObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (Ctr_PropertyClassList)d;
            panel.BuildUI(e.NewValue);
        }

        private void BuildUI(object target)
        {
            mRoot.Children.Clear();
            if (target == null) return;

            IEnumerable<GroupedProps> props = target.GetType().GetProperties()
            .Select(p => new PropMeta(p, p.GetCustomAttribute<ToolPropertyAttribute>()))
            .Where(x => PropertyEditorBindingHelper.IsDisplayableProperty(x.Prop, x.Attr))
            .GroupBy(x => x.Attr.Group)
            .OrderBy(g => g.Key)
            .Select(g => new GroupedProps
            {
                Group = g.Key,
                Items = g.OrderBy(x => x.Attr.Order).ToList()
            });

            foreach (var group in props)
            {
                Expander expander = null;
                StackPanel groupPanel = new StackPanel() { };

                if (group.Group != null)//没抬头，默认放一起
                {
                    expander = new Expander { Header = group.Group, IsExpanded = false, Margin = new Thickness(5)};
                    expander.Content = groupPanel;
                }
                else
                {
                    groupPanel.Background = Brushes.Transparent;
                }

                foreach (var item in group.Items)
                {
                    var grid = new Grid { Margin = new Thickness(5), Background = Brushes.Transparent };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    // 属性标题
                    var tb = new TextBlock
                    {
                        Text = item.Attr.DisplayName,
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand
                    };

                    Grid.SetColumn(tb, 0);

                    tb.MouseDown += (s, e) =>
                    {
                        // 默认描述
                        string desc = item.Attr.Description;

                        // 如果是枚举类型，附加上枚举值及说明
                        if (item.Prop.PropertyType.IsEnum)
                        {
                            var enumValues = Enum.GetValues(item.Prop.PropertyType);
                            StringBuilder enumDescriptions = new StringBuilder();
                            enumDescriptions.AppendLine("\n可选值:");

                            foreach (var val in enumValues)
                            {
                                string name = val.ToString();
                                var field = item.Prop.PropertyType.GetField(name);
                                var descAttr = field.GetCustomAttribute<DescriptionAttribute>();
                                string enumDesc = descAttr != null ? descAttr.Description : "";
                                enumDescriptions.AppendLine($"- {name} {(!string.IsNullOrEmpty(enumDesc) ? $"({enumDesc})" : "")}");
                            }

                            desc += enumDescriptions.ToString();
                        }


                        var tt = new ToolTip
                        {
                            Content = new TextBlock
                            {
                                Text = desc,
                                TextWrapping = TextWrapping.Wrap,
                                MaxWidth = 300
                            },
                            Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse
                        };
                        tb.ToolTip = tt;
                        tt.IsOpen = true;

                        // 自动关闭：鼠标移出时
                        tb.MouseLeave += (s2, e2) => tt.IsOpen = false;
                    };

                    // 绑定编辑器
                    FrameworkElement editor = CreateEditor(item.Prop, item.Attr, target);

                    switch (item.Attr.EditorType)
                    {
                        case E_PropertyEditorType.Text:
                        case E_PropertyEditorType.Int:
                        case E_PropertyEditorType.Double:
                        case E_PropertyEditorType.SingleSelect:
                        case E_PropertyEditorType.MultiSelect:
                        case E_PropertyEditorType.Checkbox:
                        case E_PropertyEditorType.Button:
                            Grid.SetColumn(editor, 1);
                            break;
                        case E_PropertyEditorType.DataGridList:
                            grid.RowDefinitions.Add(new RowDefinition());
                            grid.RowDefinitions.Add(new RowDefinition());
                            Grid.SetColumnSpan(editor, 2);
                            Grid.SetRow(editor, 1);
                            break;
                        default:
                            break;
                    }

                    grid.Children.Add(tb);
                    grid.Children.Add(editor);

                    groupPanel.Children.Add(grid);
                }


                if (group.Group != null)//没抬头，默认放一起
                {
                    mRoot.Children.Add(expander);
                }
                else
                {
                    mRoot.Children.Add(groupPanel);
                }
            }
        }


        private FrameworkElement CreateEditor(PropertyInfo prop, ToolPropertyAttribute attr, object target)
        {
            FrameworkElement editor = null;

            bool propIsSetPublic = IsEditableProperty(prop);

            if (!propIsSetPublic && attr.EditorType != E_PropertyEditorType.Button)
                return CreateReadOnlyEditor(prop, target);

            switch (attr.EditorType)
            {
                case E_PropertyEditorType.Int:
                    var nmb = new NumericUpDown
                    {
                        ValueStep = Convert.ToInt32(attr.Step == 0 ? 1 : attr.Step),
                        Maximum = Convert.ToInt32(double.IsNaN(attr.Max) ? int.MaxValue : attr.Max),
                        Minimum = Convert.ToInt32(double.IsNaN(attr.Min) ? int.MinValue : attr.Min)
                    };
                    BindingOperations.SetBinding(nmb, NumericUpDown.ValueProperty,
                        new Binding(prop.Name)
                        {
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
                    var dm = new DecimalUpDown
                    {
                        ValueStep = Convert.ToDecimal(attr.Step == 0 ? 0.001 : attr.Step),
                        Maximum = double.IsNaN(attr.Max) ? decimal.MaxValue : Convert.ToDecimal(attr.Max),
                        Minimum = double.IsNaN(attr.Min) ? decimal.MinValue : Convert.ToDecimal(attr.Min)
                    };
                    BindingOperations.SetBinding(dm, DecimalUpDown.ValueProperty,
                        new Binding(prop.Name)
                        {
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
                    var tb = new TextBox { Margin = new Thickness(5) };
                    Binding bd = new Binding(prop.Name)
                    {
                        Source = target,
                        Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
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
                    var cb = new ComboBox { Margin = new Thickness(5) };
                    if (prop.PropertyType.IsEnum)
                    {
                        cb.ItemsSource = Enum.GetValues(prop.PropertyType);
                    }
                    else
                    {
                        cb.ItemsSource = attr.Options;
                    }
                    BindingOperations.SetBinding(cb, ComboBox.SelectedItemProperty,
                        new Binding(prop.Name)
                        {
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
                    var mcb = new Ctr_WPF_F48.Ctr_MultiSelectComboBox
                    {
                        Margin = new Thickness(5)
                    };

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
                    else if (attr.Options != null)
                    {
                        mcb.ItemsSource = attr.Options;
                    }

                    // 绑定 SelectedItems 到对象的属性
                    BindingOperations.SetBinding(mcb, Ctr_WPF_F48.Ctr_MultiSelectComboBox.SelectedItemsProperty,
                        new Binding(prop.Name)
                        {
                            Source = target,
                            Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay
                        });
                    editor = mcb;
                    editor.IsEnabled = propIsSetPublic;
                    break;
                case E_PropertyEditorType.Checkbox:
                    var chk = new CheckBox { Margin = new Thickness(5) };
                    BindingOperations.SetBinding(chk, CheckBox.IsCheckedProperty,
                        new Binding(prop.Name) { Source = target, Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay });
                    editor = chk;
                    editor.IsEnabled = propIsSetPublic;
                    break;
                case E_PropertyEditorType.Button:
                    var btn = new Button { Content = attr.DisplayName, Margin = new Thickness(5) };
                    if (prop.PropertyType == typeof(ICommand))
                    {
                        var binding = new Binding(prop.Name)
                        {
                            Source = target,
                            Mode = BindingMode.OneWay
                        };
                        BindingOperations.SetBinding(btn, Button.CommandProperty, binding);
                    }
                    editor = btn; break;
                case E_PropertyEditorType.DataGridList:
                    var dg = new DataGrid
                    {
                        AutoGenerateColumns = true,   // 关键点：让 DataGrid 自动生成列
                        CanUserAddRows = true,
                        CanUserDeleteRows = true,
                        IsReadOnly = !propIsSetPublic,
                        Margin = new Thickness(5),
                        CanUserResizeColumns = false,
                        CanUserSortColumns = false,
                        CanUserReorderColumns = false,
                        HeadersVisibility = DataGridHeadersVisibility.All
                    };
                    dg.AutoGeneratingColumn += (s, e) => PropertyEditorBindingHelper.ApplyAutoGeneratedColumnRules(e);
                    dg.LoadingRow += (s, e) =>
                    {
                        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
                    };
                    // 绑定 ItemsSource
                    BindingOperations.SetBinding(dg, DataGrid.ItemsSourceProperty,
                        new Binding(prop.Name)
                        {
                            Source = target,
                            Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay
                        });

                    editor = dg; break;

            }

            return editor;
        }

        private static bool IsEditableProperty(PropertyInfo prop)
        {
            return PropertyEditorBindingHelper.IsEditableProperty(prop);
        }

        private FrameworkElement CreateReadOnlyEditor(PropertyInfo prop, object target)
        {
            var textBlock = new TextBlock
            {
                Margin = new Thickness(5),
                Padding = new Thickness(6, 3, 6, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap
            };

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
}
