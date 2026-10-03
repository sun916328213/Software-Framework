// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：Ctr_MultiSelectComboBox。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Vision.Controls
{
    /// <summary>
    /// Ctr_MultiSelectComboBox 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// 继承或实现的契约：ComboBox。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class Ctr_MultiSelectComboBox : ComboBox
    {
        /// <summary>
        /// 初始化 Ctr_MultiSelectComboBox 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SetBinding、AddHandler、UpdateText、BeginInvoke、SyncChecks；具体条件和先后次序见方法体。
        /// </summary>
        public Ctr_MultiSelectComboBox()
        {
            this.IsEditable = true;
            this.StaysOpenOnEdit = true;
            this.IsReadOnly = true;

            // 下拉项模板：CheckBox，绑定到 SelectedItems 集合
            this.ItemTemplate = new DataTemplate
            {
                VisualTree = new FrameworkElementFactory(typeof(CheckBox))
            };
            var checkFactory = this.ItemTemplate.VisualTree;
            checkFactory.SetBinding(CheckBox.ContentProperty, new Binding("."));
            checkFactory.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(OnItemChecked));
            checkFactory.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(OnItemUnchecked));

            this.DropDownClosed += (s, e) => UpdateText();
            this.DropDownOpened += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(SyncChecks), System.Windows.Threading.DispatcherPriority.Loaded);
            };

            this.ItemContainerGenerator.StatusChanged += (s, e) =>
            {
                if (this.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    SyncChecks();
                }
            };
        }

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(nameof(SelectedItems), typeof(IList), typeof(Ctr_MultiSelectComboBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

        public IList SelectedItems
        {
            get => (IList)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnSelectedItemsChanged。
        /// 正文中的关键协作调用包括 UpdateText；具体条件和先后次序见方法体。
        /// </summary>
        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_MultiSelectComboBox cb)
            {
                cb.UpdateText();
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnItemChecked。
        /// 正文中的关键协作调用包括 Contains、Add、UpdateText；具体条件和先后次序见方法体。
        /// </summary>
        private void OnItemChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                var item = chk.DataContext;
                if (SelectedItems != null && !SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                    UpdateText();
                }
            }
        }

        /// <summary>
        /// 处理对应通知、事件或生命周期回调；对应入口为 OnItemUnchecked。
        /// 正文中的关键协作调用包括 Contains、Remove、UpdateText；具体条件和先后次序见方法体。
        /// </summary>
        private void OnItemUnchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                var item = chk.DataContext;
                if (SelectedItems != null && SelectedItems.Contains(item))
                {
                    SelectedItems.Remove(item);
                    UpdateText();
                }
            }
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateText。
        /// 正文中的关键协作调用包括 Join、Select、Cast、ToString；具体条件和先后次序见方法体。
        /// </summary>
        private void UpdateText()
        {
            if (SelectedItems == null || SelectedItems.Count == 0)
            {
                this.Text = string.Empty;
                return;
            }

            this.Text = string.Join(", ", SelectedItems.Cast<object>().Select(x => x.ToString()));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SyncChecks。
        /// 正文中的关键协作调用包括 ContainerFromItem、FindVisualChild、Contains；具体条件和先后次序见方法体。
        /// </summary>
        private void SyncChecks()
        {
            if (SelectedItems == null) return;

            foreach (var item in this.Items)
            {
                var container = this.ItemContainerGenerator.ContainerFromItem(item) as ComboBoxItem;
                if (container != null)
                {
                    var chk = FindVisualChild<CheckBox>(container);
                    if (chk != null)
                    {
                        chk.IsChecked = SelectedItems.Contains(item);
                    }
                }
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 FindVisualChild。
        /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、FindVisualChild；具体条件和先后次序见方法体。
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T target)
                    return target;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
