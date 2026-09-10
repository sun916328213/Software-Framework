using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlowEditor.Core;
using FlowEditor.Models;
using FlowEditor.Nodes.FlowControl;
using FlowEditor.ViewModels;
using FlowEditor.Views;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using WPFCustomControls.PropertyGrid.Models;
using WPFCustomControls.PropertyGrid.ViewModels;
using Xunit;

namespace FlowEditorTest;

public class PortDisplayNameTests
{
    [Fact]
    public void LoopNode_InitialIterationsInput_UsesChineseDisplayNameAndPropertyBinding()
    {
        var node = new LoopNode();

        var port = Assert.Single(node.CustomInputPorts,
            p => p.Name == "循环次数" && p.PropertyName == nameof(LoopNode.Iterations));
        Assert.Equal("循环次数", port.Name);
    }

    [Fact]
    public void LoopNode_ReenabledIterationsInput_UsesAttributeDisplayName()
    {
        var node = new LoopNode();
        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: false, isOutput: false);

        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: true, isOutput: false);

        var port = Assert.Single(node.CustomInputPorts,
            p => p.PropertyName == nameof(LoopNode.Iterations));
        Assert.Equal("循环次数", port.Name);
    }

    [Fact]
    public void PropertyPanel_ReenabledIterationsInput_ReloadsSingleNamedRow()
    {
        var node = new LoopNode();
        using var ports = new PortManagementViewModel(
            node,
            [nameof(LoopNode.Iterations)],
            new NullPortDialogService());

        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: false, isOutput: false);
        Assert.Empty(ports.InputPorts);

        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: true, isOutput: false);

        var row = Assert.Single(ports.InputPorts);
        Assert.Equal("循环次数", row.Name);
        Assert.Equal(nameof(LoopNode.Iterations), row.BoundPropertyName);
        Assert.True(row.ShowAsTextBlock);
    }

    [Fact]
    public void LoopNode_ChineseIterationsPort_StillWritesBoundProperty()
    {
        var node = new LoopNode();
        var port = Assert.Single(node.CustomInputPorts,
            p => p.PropertyName == nameof(LoopNode.Iterations));
        port.Value = 12;

        node.SyncInputValues();

        Assert.Equal(12, node.Iterations);
    }

    [Fact]
    public void PropertyBoundPort_WithBlankRequestedName_UsesAttributeDisplayName()
    {
        var node = new LoopNode();
        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: false, isOutput: false);

        var port = node.AddCustomInputPort(string.Empty, nameof(LoopNode.Iterations));

        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
    }

    [Fact]
    public void UnboundPort_WithBlankRequestedName_UsesVisiblePlaceholder()
    {
        var node = new LoopNode();

        var port = node.AddCustomInputPort("   ");

        Assert.Equal("未命名端口", port.Name);
    }

    [Fact]
    public void RenameCustomPort_ToBlank_IsRejectedAndKeepsExistingName()
    {
        var node = new LoopNode();
        var port = node.AddCustomInputPort("自定义输入");

        var renamed = node.RenameCustomPort(port.Id, "   ");

        Assert.False(renamed);
        Assert.Equal("自定义输入", port.Name);
    }

    [Fact]
    public void PortModel_DirectBlankAssignment_UsesVisiblePlaceholder()
    {
        var port = PortModel.CreateInput("输入");

        port.Name = "   ";

        Assert.Equal("未命名端口", port.Name);
    }

    [Fact]
    public void LegacyEnglishIterationsPort_RestoresWithoutDuplicateAndKeepsChineseName()
    {
        var document = new NodeDocumentModel
        {
            Id = "loop",
            Name = "循环",
            TypeName = typeof(LoopNode).AssemblyQualifiedName!,
            CustomInputPorts =
            {
                new CustomPortDocument { Id = "legacy-iterations", Name = "Iterations" }
            }
        };

        var node = Assert.IsType<LoopNode>(ReflectionNodeFactory.Instance.Create(document));

        var port = Assert.Single(node.CustomInputPorts);
        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
        Assert.Equal("legacy-iterations", port.Id);
    }

    [Fact]
    public void LegacyBlankIterationsPort_WithPropertyBinding_RepairsChineseName()
    {
        var document = new NodeDocumentModel
        {
            Id = "loop",
            Name = "循环",
            TypeName = typeof(LoopNode).AssemblyQualifiedName!,
            CustomInputPorts =
            {
                new CustomPortDocument
                {
                    Id = "blank-iterations",
                    Name = string.Empty,
                    BoundProperty = nameof(LoopNode.Iterations)
                }
            }
        };

        var node = Assert.IsType<LoopNode>(ReflectionNodeFactory.Instance.Create(document));

        var port = Assert.Single(node.CustomInputPorts);
        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
        Assert.Equal("blank-iterations", port.Id);
    }

    [Fact]
    public void EnablingIterationsInput_RendersVisiblePortNameInNodeView()
    {
        WpfTestHost.Run(() =>
        {
            var node = new LoopNode();
            node.SetPortConfig(nameof(LoopNode.Iterations), false, false);
            var view = new NodeView
            {
                DataContext = new NodeViewModel(node),
                Width = 160
            };
            view.Measure(new Size(160, 500));
            view.Arrange(new Rect(0, 0, 160, view.DesiredSize.Height));

            node.SetPortConfig(nameof(LoopNode.Iterations), true, false);
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            view.Measure(new Size(160, 500));
            view.Arrange(new Rect(0, 0, 160, view.DesiredSize.Height));
            view.UpdateLayout();

            var inputPorts = FindVisualChildren<ItemsControl>(view)
                .Single(control => control.Name == "InputPortsItemsControl");
            var label = FindVisualChildren<TextBlock>(inputPorts)
                .FirstOrDefault(text => text.Text == "循环次数");
            Assert.NotNull(label);
            Assert.True(label.ActualWidth >= 20, $"端口名称宽度应可见，实际为 {label.ActualWidth}");
            var foreground = Assert.IsType<SolidColorBrush>(label.Foreground).Color;
            var contrast = GetContrastRatio(foreground, Colors.White);
            Assert.True(contrast >= 4.5,
                $"端口名称与节点背景对比度应至少为 4.5，实际为 {contrast:F2}");

            using var portManagement = new PortManagementViewModel(
                node,
                [nameof(LoopNode.Iterations)],
                new NullPortDialogService());
            var row = Assert.Single(portManagement.InputPorts);
            var propertyGridResources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/WPFCustomControls;component/PropertyGrid/Views/BuiltinPropertyGridResources.xaml")
            };
            var rowPresenter = new ContentControl
            {
                Content = row,
                ContentTemplate = Assert.IsType<DataTemplate>(propertyGridResources["PG_PortRowTemplate"]),
                Width = 320
            };
            rowPresenter.Measure(new Size(320, 100));
            rowPresenter.Arrange(new Rect(0, 0, 320, rowPresenter.DesiredSize.Height));
            rowPresenter.UpdateLayout();

            var propertyPanelLabel = FindVisualChildren<TextBlock>(rowPresenter)
                .FirstOrDefault(text => text.Text == "循环次数" && text.Visibility == Visibility.Visible);
            Assert.NotNull(propertyPanelLabel);
            Assert.True(propertyPanelLabel.ActualWidth >= 20,
                $"属性面板端口名称宽度应可见，实际为 {propertyPanelLabel.ActualWidth}");
        });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        static double Luminance(Color color)
        {
            static double Linearize(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045
                    ? value / 12.92
                    : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Linearize(color.R)
                 + 0.7152 * Linearize(color.G)
                 + 0.0722 * Linearize(color.B);
        }

        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private sealed class NullPortDialogService : IPortDialogService
    {
        public PortDialogResult? ShowAddPortDialog(PortDialogRequest request) => null;

        public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request) => null;
    }
}
