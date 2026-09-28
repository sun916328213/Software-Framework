using Vision.Core;
using MaterialDesignThemes.Wpf;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Vision.Controls
{
    public class Ctr_PropertyPanel : ScrollViewer
    {
        private StackPanel mRoot;

        public static readonly DependencyProperty TargetObjectProperty =
            DependencyProperty.Register(nameof(TargetObject), typeof(object), typeof(Ctr_PropertyPanel),
                new PropertyMetadata(null, OnTargetObjectChanged));

        public object TargetObject
        {
            get => GetValue(TargetObjectProperty);
            set => SetValue(TargetObjectProperty, value);
        }

        public Ctr_PropertyPanel()
        {
            mRoot = new StackPanel();
            this.Content = mRoot;

            //HorizontalScrollBarVisibility =  ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            // 添加滚动条样式
            var scrollBarStyle = new Style(typeof(ScrollBar));
            scrollBarStyle.BasedOn = (Style)FindResource("MaterialDesignScrollBar");
            
            var buttonsVisibilitySetter = new Setter(ScrollBarAssist.ButtonsVisibilityProperty, Visibility.Collapsed);
            var thumbCornerRadiusSetter = new Setter(ScrollBarAssist.ThumbCornerRadiusProperty, new CornerRadius(4));
            var thumbHeightSetter = new Setter(ScrollBarAssist.ThumbHeightProperty, 12.0);
            var thumbWidthSetter = new Setter(ScrollBarAssist.ThumbWidthProperty, 8.0);
            
            scrollBarStyle.Setters.Add(buttonsVisibilitySetter);
            scrollBarStyle.Setters.Add(thumbCornerRadiusSetter);
            scrollBarStyle.Setters.Add(thumbHeightSetter);
            scrollBarStyle.Setters.Add(thumbWidthSetter);
            
            this.Resources.Add(typeof(ScrollBar), scrollBarStyle);
        }

        private static void OnTargetObjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (Ctr_PropertyPanel)d;
            panel.BuildUI(e.NewValue);
        }

        private void BuildUI(object target)
        {
            mRoot.Children.Clear();
            if (target == null) return;

            var props = target.GetType().GetProperties()
                .Select(p => new { Prop = p, Attr = p.GetCustomAttribute<ToolPropertyAttribute>() })
                .Where(x => PropertyEditorBindingHelper.IsDisplayableProperty(x.Prop, x.Attr))
                .GroupBy(x => x.Attr.Group)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Group = g.Key,
                    Items = g.OrderBy(x => x.Attr.Order).ToList()
                }); ;

            //添加属性分组
            foreach (var group in props)
            {
                Expander expander = new Expander
                {
                    Header = group.Group,
                    IsExpanded = false,
                };

                // 创建外部Border以实现更精细的圆角效果
                Border outerBorder = new Border() {
                    Name = "outerBorder",
                    CornerRadius = new CornerRadius(12),
                    Margin = new Thickness(4),
                    Background = expander.Background,
                    Padding = new Thickness(4) // 添加内边距以改善视觉效果
                };

                StackPanel groupPanel = new StackPanel() ;
 
 
                //添加单个属性
                foreach (var item in group.Items)
                {

                    var grid = new Grid();

                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    // 属性标题
                    var tb = new TextBlock
                    {
                        Text = item.Attr.DisplayName + ":",
                        VerticalAlignment= VerticalAlignment.Center,
                        Margin = new Thickness(10, 4, 4, 4)
                    };

                    Grid.SetColumn(tb, 0);

                    tb.MouseDown += (s, e) =>
                    {
                        ToolTip tt;

                        if (tb.ToolTip == null)
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


                            tt = new ToolTip
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
                            // 自动关闭：鼠标移出时
                            tb.MouseLeave += (s2, e2) => tt.IsOpen = false;
                            tt.IsOpen = true;
                        }
                        else
                        {
                            tt = (ToolTip)tb.ToolTip;
                            tt.IsOpen = true;
                        }
                    };
               
                    FrameworkElement editor = CreateEditor(item.Prop, item.Attr, target);  // 绑定编辑器

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
                        case E_PropertyEditorType.DataClassList:
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


                //将单个属性集合添加到属性分组里面
                if (group.Group != null)//没抬头，默认放一起
                {
                    expander.Content = groupPanel;
                    // 将Expander放入外部Border中以实现圆角效果
                    outerBorder.Child = expander;
                    // 添加外部Border而不是直接添加expander
                    mRoot.Children.Add(outerBorder);
                }
                else
                { 
                    outerBorder.Child = groupPanel;
                    mRoot.Children.Add(outerBorder);
                }
            }
        }


        private static void StaticButtonDo(object sender, EventArgs e)
        {
            var act = (Action<object, EventArgs>)((Button)sender).Tag;
            act(sender, e);
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
                        Margin = new Thickness(5),
                        ValueStep = Convert.ToInt32(attr.Step == 0 ? 1 : attr.Step),
                        Maximum = Convert.ToInt32(double.IsNaN(attr.Max) ? int.MaxValue : attr.Max),
                        Minimum = Convert.ToInt32(double.IsNaN(attr.Min) ? int.MinValue : attr.Min) };
                     
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
                    var dm = new DecimalUpDown {
                        Margin = new Thickness(5),
                        ValueStep = Convert.ToDecimal(attr.Step == 0 ? 0.001 : attr.Step),
                        Maximum = double.IsNaN(attr.Max) ? decimal.MaxValue : Convert.ToDecimal(attr.Max),
                        Minimum = double.IsNaN(attr.Min) ? decimal.MinValue : Convert.ToDecimal(attr.Min) };
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
                    else if (attr.Options != null && attr.Options.Length != 0)
                    {
                        cb.ItemsSource = attr.Options;
                    }
                    else if (attr.ListItemsSourceType != null && !string.IsNullOrWhiteSpace(attr.ListItemsSourceName)) //来源不为空，且来源必须为可遍历的类型
                    {
                        const BindingFlags BF = BindingFlags.Public | BindingFlags.Static;

                        PropertyInfo source = attr.ListItemsSourceType.GetProperty(attr.ListItemsSourceName, BF);
                        var val = source.GetValue(null);

                        if (val is System.Collections.IEnumerable enumerable)
                        {
                            cb.ItemsSource = enumerable;
                        }

                    }
                    BindingOperations.SetBinding(cb, ComboBox.SelectedItemProperty,
                        new Binding(prop.Name) { 
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
                    var mcb = new Vision.Controls.Ctr_MultiSelectComboBox
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
                    BindingOperations.SetBinding(mcb, Vision.Controls.Ctr_MultiSelectComboBox.SelectedItemsProperty,
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
                case E_PropertyEditorType.DataClassList:

                    var collectionObj = prop.GetValue(target) as System.Collections.IList;  //获取列表对象
                    var collection =  prop.GetValue(target) as INotifyCollectionChanged;    //给他添加变化回调
                    //if (collection != null)
                    //{
                    //    collection.CollectionChanged += OnCollectionChanged;
                    //}

                    var dgc = new DataGrid
                    {
                        AutoGenerateColumns = false,
                        CanUserAddRows = false,
                        CanUserDeleteRows = false,
                        IsReadOnly = !propIsSetPublic,
                        Margin = new Thickness(5),
                        CanUserResizeColumns = false,
                        CanUserSortColumns = false,
                        CanUserReorderColumns = false,
                        RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected,
                        HeadersVisibility = DataGridHeadersVisibility.All
                    };

                    //显示行序号
                    dgc.HeadersVisibility = DataGridHeadersVisibility.All;
                    dgc.LoadingRow += (s, e) =>
                    {
                        e.Row.Header = (e.Row.GetIndex() + 1).ToString();
                    };

                    string propName = attr.BindingProp;
                    dgc.Columns.Add(new System.Windows.Controls.DataGridTextColumn()
                    {
                        Header = "名称",
                        Binding = new Binding(propName) { Mode = BindingMode.OneWay },// 只读显示 ToString()
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });

                    // 绑定 ItemsSource 集合
                    BindingOperations.SetBinding(dgc, DataGrid.ItemsSourceProperty,
                        new Binding(prop.Name)
                        {
                            Source = target,
                            Mode = propIsSetPublic ? BindingMode.TwoWay : BindingMode.OneWay
                        });

                    if (!string.IsNullOrEmpty(attr.BindingIsHit))
                    {
                        var style = new Style(typeof(DataGridRow))
                        {
                            BasedOn = (Style)Application.Current.TryFindResource("MaterialDesignDataGridRow")
                        };
                        var bindingIsHit = new Binding(attr.BindingIsHit)
                        {
                            Mode = BindingMode.OneWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        };
                        style.Setters.Add(new Setter(DataGridRow.IsSelectedProperty, bindingIsHit));
                        dgc.RowStyle = style;
                    }

                    var f = new FrameworkElementFactory(typeof(Vision.Controls.Ctr_PropertyPanel));
                    // 属性面板绑定到当前行对象本身
                    f.SetBinding(Vision.Controls.Ctr_PropertyPanel.TargetObjectProperty, new Binding("."));
                    var details = new DataTemplate { VisualTree = f };
                    dgc.RowDetailsTemplate = details;

                    // 监听集合变化：结构变动后重算行号，增删行由绑定自动完成
                    if (collection != null)
                    {
                        collection.CollectionChanged += (s, e) =>
                        {
                            // 只做轻量处理，比如重编号
                            dgc.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                for (int i = 0; i < dgc.Items.Count; i++)
                                    if (dgc.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row)
                                        row.Header = (i + 1).ToString();
                            }));
                        };
                    }

                    editor = dgc; break;
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


    static class ButtonBinder
    {
        // 缓存：(Type, MethodName)  Action<object,EventArgs>
        private static readonly ConcurrentDictionary<(Type, string), Action<object, EventArgs>> _cache = new ConcurrentDictionary<(Type, string), Action<object, EventArgs>>();

        public static Action<object, EventArgs> Resolve(object target, string methodName)
        {
            var key = (target.GetType(), methodName);
            return _cache.GetOrAdd(key, _ =>
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                // 1) 精确匹配 (object, EventArgs)
                var mi = target.GetType().GetMethod(methodName, flags, null, new[] { typeof(object), typeof(EventArgs) }, null);
                if (mi == null)
                    throw new Exception($"{target.GetType()} 不存在当前类型的方法({methodName})");
                return (Action<object, EventArgs>)Delegate.CreateDelegate(typeof(Action<object, EventArgs>), target, mi);
            });
        }
    }
}
