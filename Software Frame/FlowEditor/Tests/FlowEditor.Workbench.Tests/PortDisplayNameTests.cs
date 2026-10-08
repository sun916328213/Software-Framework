// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：PortDisplayNameTests、NullPortDialogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Controls、System.Windows.Media、System.Windows.Threading、FlowEditor.Controls.Core、FlowEditor.Core。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FlowEditor.Controls.Core;
using FlowEditor.Core;
using FlowEditor.Runtime;
using FlowEditor.Controls.Models;
using FlowEditor.Core.Models;
using FlowEditor.Controls.Nodes.FlowControl;
using FlowEditor.Controls.ViewModels;
using FlowEditor.Controls.Views;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using WPFCustomControls.PropertyGrid.Core.Models;
using WPFCustomControls.PropertyGrid.Core.ViewModels;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>
/// PortDisplayNameTests 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public class PortDisplayNameTests
{
    /// <summary>
    /// 测试相关入口 LoopNode_InitialIterationsInput_UsesChineseDisplayNameAndPropertyBinding；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、nameof、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoopNode_InitialIterationsInput_UsesChineseDisplayNameAndPropertyBinding()
    {
        var node = new LoopNode();

        var port = Assert.Single(node.CustomInputPorts,
            p => p.Name == "循环次数" && p.PropertyName == nameof(LoopNode.Iterations));
        Assert.Equal("循环次数", port.Name);
    }

    /// <summary>
    /// 测试相关入口 LoopNode_ReenabledIterationsInput_UsesAttributeDisplayName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetPortConfig、nameof、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 PropertyPanel_ReenabledIterationsInput_ReloadsSingleNamedRow；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、SetPortConfig、Empty、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 LoopNode_ChineseIterationsPort_StillWritesBoundProperty；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Single、nameof、SyncInputValues、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 PropertyBoundPort_WithBlankRequestedName_UsesAttributeDisplayName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 SetPortConfig、nameof、AddCustomInputPort、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PropertyBoundPort_WithBlankRequestedName_UsesAttributeDisplayName()
    {
        var node = new LoopNode();
        node.SetPortConfig(nameof(LoopNode.Iterations), isInput: false, isOutput: false);

        var port = node.AddCustomInputPort(string.Empty, nameof(LoopNode.Iterations));

        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
    }

    /// <summary>
    /// 测试相关入口 UnboundPort_WithBlankRequestedName_UsesVisiblePlaceholder；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddCustomInputPort、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void UnboundPort_WithBlankRequestedName_UsesVisiblePlaceholder()
    {
        var node = new LoopNode();

        var port = node.AddCustomInputPort("   ");

        Assert.Equal("未命名端口", port.Name);
    }

    /// <summary>
    /// 测试相关入口 RenameCustomPort_ToBlank_IsRejectedAndKeepsExistingName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 AddCustomInputPort、RenameCustomPort、False、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RenameCustomPort_ToBlank_IsRejectedAndKeepsExistingName()
    {
        var node = new LoopNode();
        var port = node.AddCustomInputPort("自定义输入");

        var renamed = node.RenameCustomPort(port.Id, "   ");

        Assert.False(renamed);
        Assert.Equal("自定义输入", port.Name);
    }

    /// <summary>
    /// 测试相关入口 PortModel_DirectBlankAssignment_UsesVisiblePlaceholder；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 CreateInput、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void PortModel_DirectBlankAssignment_UsesVisiblePlaceholder()
    {
        var port = PortModel.CreateInput("输入");

        port.Name = "   ";

        Assert.Equal("未命名端口", port.Name);
    }

    /// <summary>
    /// 测试相关入口 LegacyEnglishIterationsPort_RestoresWithoutDuplicateAndKeepsChineseName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 IsType、Create、Single、Equal、nameof；具体条件和先后次序见方法体。
    /// </summary>
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

        var node = Assert.IsType<LoopNode>(new ReflectionNodeFactory().Create(document));

        var port = Assert.Single(node.CustomInputPorts);
        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
        Assert.Equal("legacy-iterations", port.Id);
    }

    /// <summary>
    /// 测试相关入口 LegacyBlankIterationsPort_WithPropertyBinding_RepairsChineseName；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 nameof、IsType、Create、Single、Equal；具体条件和先后次序见方法体。
    /// </summary>
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

        var node = Assert.IsType<LoopNode>(new ReflectionNodeFactory().Create(document));

        var port = Assert.Single(node.CustomInputPorts);
        Assert.Equal("循环次数", port.Name);
        Assert.Equal(nameof(LoopNode.Iterations), port.PropertyName);
        Assert.Equal("blank-iterations", port.Id);
    }

    /// <summary>
    /// 测试相关入口 EnablingIterationsInput_RendersVisiblePortNameInNodeView；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Run、SetPortConfig、nameof、Measure、Arrange；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 FindVisualChildren；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、FindVisualChildren；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 GetContrastRatio；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Pow、Linearize、Max、Luminance、Min；具体条件和先后次序见方法体。
    /// </summary>
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

    /// <summary>
    /// NullPortDialogService 的类型定义。FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：IPortDialogService。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class NullPortDialogService : IPortDialogService
    {
        /// <summary>
        /// 测试相关入口 ShowAddPortDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public PortDialogResult? ShowAddPortDialog(PortDialogRequest request) => null;

        /// <summary>
        /// 测试相关入口 ShowModifyBindingDialog；准备条件、协作调用与结果检查以方法正文为准。
        /// </summary>
        public PortDialogResult? ShowModifyBindingDialog(PortDialogRequest request) => null;
    }
}
