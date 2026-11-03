using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Vision.Controls;
using Vision.Controls.Presentation;
using Vision.Controls.View;
using Vision.Core;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;

namespace Vision.Workbench.Tests.Controls;

[Collection("WorkbenchAppBoot")]
[Trait("Category", "Regression")]
public sealed class VisionPropertyPanelTests(WorkbenchAppFixture host)
{
    [Fact]
    public void IndependentView_UsesSharedCoreRows_AdaptsToolMetadata_AndEditsOriginalDto()
    {
        host.Invoke(() =>
        {
            var target = new Parameters();
            var panel = new VisionPropertyPanel { TargetObject = target, ExpandGroups = true };
            var window = Show(panel);
            try
            {
                Assert.IsType<PropertyGridHostViewModel>(panel.Model);
                Assert.Same(target, panel.Model.TargetObject);
                var group = Assert.Single(panel.Model.Groups); Assert.Equal("参数组", group.GroupName);
                var row = Assert.IsAssignableFrom<PropertyItemViewModel>(group.Items.Single(i => i.PropertyName == nameof(Parameters.Count)));
                Assert.IsType<VisionPropertyItem>(row); Assert.Equal("数量", row.DisplayName);
                Assert.Equal(0, row.NumericMin); Assert.Equal(20, row.NumericMax); Assert.Equal(2, row.NumericStep);
                Assert.Null(typeof(Parameters).GetProperty(nameof(Parameters.Count))!.GetCustomAttribute<NodePropertyAttribute>());
                Assert.DoesNotContain(Descendants(panel), item => item.GetType().FullName == "WPFCustomControls.PropertyGrid.Views.PropertyGridHost");
                var number = Assert.Single(Descendants(panel).OfType<NumericUpDown>());
                var changed = 0; panel.ParametersChanged += (_, _) => changed++;
                number.SetCurrentValue(NumericUpDown.ValueProperty, 8); Drain();
                Assert.Equal(8, target.Count); Assert.Equal(8, row.Value); Assert.True(changed > 0);
                var before = changed; panel.Model.SearchText = "数量"; Drain();
                Assert.Single(Assert.Single(panel.Model.Groups).Items); Assert.Equal(before, changed); Assert.Equal(8, target.Count);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void DynamicStylesAndColumnWidth_UpdateExistingEditors_WithoutRebuildingOrWritingParameters()
    {
        host.Invoke(() =>
        {
            var target = new Parameters(); var panel = new VisionPropertyPanel { TargetObject = target, ExpandGroups = true };
            var window = Show(panel);
            try
            {
                var label = Descendants(panel).OfType<TextBlock>().Single(t => t.Text == "数量:");
                var editor = Assert.Single(Descendants(panel).OfType<NumericUpDown>());
                var row = panel.Model.Groups[0].Items[0];
                var changed = 0; panel.ParametersChanged += (_, _) => changed++;
                var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 23.0));
                panel.Resources["Vision.PropertyPanel.Label"] = style;
                panel.Resources["Vision.PropertyPanel.LabelWidth"] = new GridLength(190);
                var numericStyle = new Style(typeof(NumericUpDown)); numericStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(12)));
                panel.Resources["Vision.PropertyPanel.Integer"] = numericStyle; Drain();
                Assert.Equal(23, label.FontSize); Assert.Equal(new Thickness(12), editor.Margin);
                var grid = Assert.IsType<Grid>(VisualTreeHelper.GetParent(label)); Assert.Equal(new GridLength(190), grid.ColumnDefinitions[0].Width);
                Assert.Same(editor, Assert.Single(Descendants(panel).OfType<NumericUpDown>()));
                Assert.Same(row, panel.Model.Groups[0].Items[0]); Assert.Same(target, panel.TargetObject);
                Assert.Equal(0, target.Count); Assert.Equal(0, changed);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void GroupCard_ClipsChildBackgroundsToRoundedCorners_AndUpdatesRadiusWithoutRebuilding()
    {
        host.Invoke(() =>
        {
            var target = new Parameters();
            var panel = new VisionPropertyPanel { TargetObject = target, ExpandGroups = true };
            var window = Show(panel);
            try
            {
                var groupStyle = Assert.IsType<Style>(panel.FindResource("Vision.PropertyPanel.Group"));
                var group = Assert.Single(Descendants(panel).OfType<FrameworkElement>(), e => ReferenceEquals(e.Style, groupStyle));
                var expander = Assert.Single(Descendants(group).OfType<Expander>());
                expander.SetCurrentValue(Control.BackgroundProperty, Brushes.Magenta);
                Drain();
                var contentHost = Assert.IsAssignableFrom<UIElement>(VisualTreeHelper.GetParent(expander));
                var clip = Assert.IsType<RectangleGeometry>(contentHost.Clip);
                Assert.Equal(12, clip.RadiusX); Assert.Equal(12, clip.RadiusY);
                Assert.False(clip.FillContains(new Point(1, 1)));
                Assert.True(clip.FillContains(new Point(clip.Rect.Width / 2, clip.Rect.Height / 2)));
                var card = Assert.IsType<Card>(group); Assert.True(card.ClipContent);
                var editor = Assert.Single(Descendants(panel).OfType<NumericUpDown>());
                var changed = 0; panel.ParametersChanged += (_, _) => changed++;
                var style = new Style(typeof(Card), groupStyle);
                style.Setters.Add(new Setter(Card.UniformCornerRadiusProperty, 24.0));
                panel.Resources["Vision.PropertyPanel.Group"] = style; Drain();
                clip = Assert.IsType<RectangleGeometry>(contentHost.Clip);
                Assert.Equal(24, clip.RadiusX); Assert.Equal(24, clip.RadiusY);
                window.Width = 420; Drain();
                clip = Assert.IsType<RectangleGeometry>(contentHost.Clip);
                Assert.Equal(24, clip.RadiusX);
                Assert.False(clip.FillContains(new Point(1, 1)));
                Assert.Same(editor, Assert.Single(Descendants(panel).OfType<NumericUpDown>()));
                Assert.Same(target, panel.TargetObject); Assert.Equal(0, target.Count); Assert.Equal(0, changed);
            }
            finally { window.Close(); Drain(); }
        });
    }

    [Fact]
    public void UnloadAndReplacement_DisposeSharedRowSubscriptions_WithoutOwningTheDto()
    {
        host.Invoke(() =>
        {
            var target = new Parameters(); var replacement = new Parameters();
            var panel = new VisionPropertyPanel { TargetObject = target, ExpandGroups = true };
            var window = Show(panel);
            try
            {
                var old = panel.Model.Groups[0].Items.Single(row => row.PropertyName == nameof(Parameters.Count));
                var notifications = 0; old.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PropertyItemViewModel.Value)) notifications++; };
                target.Count = 2; Assert.True(notifications > 0);
                window.Content = null; Drain(); var before = notifications;
                Assert.Empty(panel.Model.Groups); Assert.Null(panel.Model.TargetObject); Assert.Same(target, panel.TargetObject);
                target.Count = 4; Drain(); Assert.Equal(before, notifications);
                window.Content = panel; Drain();
                Assert.Same(target, panel.Model.TargetObject); Assert.Equal(4, Assert.Single(Descendants(panel).OfType<NumericUpDown>()).Value);
                panel.TargetObject = replacement; Drain(); before = notifications;
                target.Count = 6; Drain(); Assert.Equal(before, notifications);
                Assert.Equal(0, Assert.Single(Descendants(panel).OfType<NumericUpDown>()).Value);
                panel.PropertyNames = nameof(Parameters.Name); Drain();
                Assert.Equal(nameof(Parameters.Name), Assert.Single(Assert.Single(panel.Model.Groups).Items).PropertyName);
            }
            finally { window.Close(); Drain(); }
        });
    }

    private sealed class Parameters : INotifyPropertyChanged
    {
        private int _count;
        [ToolProperty(DisplayName = "数量", Group = "参数组", Order = 0, EditorType = E_PropertyEditorType.Int, Min = 0, Max = 20, Step = 2)]
        public int Count { get => _count; set { _count = value; PropertyChanged?.Invoke(this, new(nameof(Count))); } }
        [ToolProperty(DisplayName = "名称", Group = "参数组", Order = 1, EditorType = E_PropertyEditorType.Text)]
        public string Name { get; set; } = "original";
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private static Window Show(FrameworkElement content)
    {
        var window = new Window { Content = content, Width = 800, Height = 600, Left = -10000, Top = -10000, ShowInTaskbar = false };
        window.Show(); Drain(); return window;
    }
    private static void Drain() => Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
