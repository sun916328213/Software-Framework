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
    public class Ctr_MultiSelectComboBox : ComboBox
    {
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

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Ctr_MultiSelectComboBox cb)
            {
                cb.UpdateText();
            }
        }

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

        private void UpdateText()
        {
            if (SelectedItems == null || SelectedItems.Count == 0)
            {
                this.Text = string.Empty;
                return;
            }

            this.Text = string.Join(", ", SelectedItems.Cast<object>().Select(x => x.ToString()));
        }

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
