// 【中文阅读说明】
// ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：InteractionTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Theme、Prism.Ioc、Prism.Regions、ScriptEditor.Controls.ViewModels、ScriptEditor.Controls.Views、System.Windows。导入不代表所有类型都会在运行时加载。
using Extension.UI.Theme;
using Prism.Ioc;
using Prism.Regions;
using ScriptEditor.Controls.ViewModels;
using ScriptEditor.Controls.Views;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScriptEditor.Workbench.Tests;

/// <summary>
/// InteractionTests 的类型定义。ScriptEditor 模块：脚本持久化模型、运行时编译执行和编辑器协作；脚本 ID、引用及断点属于需要保留的状态。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("WorkbenchAppBoot")]
public sealed class InteractionTests(WorkbenchAppFixture host)
{
    /// <summary>
    /// 验证 RealManagerButtons_RefreshEmptyAndBoundaryAvailability 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、IsType、Single、Resolve、Empty；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RealManagerButtons_RefreshEmptyAndBoundaryAvailability()
    {
        host.Invoke(() =>
        {
            var page = Assert.IsType<ScriptManagerView>(Assert.Single(ContainerLocator.Container.Resolve<IRegionManager>().Regions["ScriptRegion"].ActiveViews));
            var vm = Assert.IsType<ScriptManagerViewModel>(page.DataContext);
            Assert.Empty(vm.Items);
            Assert.False(Button(page, "依次执行所有脚本").IsEnabled);
            Invoke(page, "添加脚本 (Ctrl+N)");
            Assert.Single(vm.Items);
            Assert.True(Button(page, "依次执行所有脚本").IsEnabled);
            Assert.False(Button(page, "上移").IsEnabled);
            Assert.False(Button(page, "下移").IsEnabled);
            var original = vm.Model.SelectedItem;
            Invoke(page, "复制脚本");
            Assert.Equal(2, vm.Items.Count);
            Assert.True(Button(page, "上移").IsEnabled);
            Assert.False(Button(page, "下移").IsEnabled);
            Invoke(page, "上移");
            Assert.Same(original, vm.Items[1]);
            Assert.False(Button(page, "上移").IsEnabled);
            Assert.True(Button(page, "下移").IsEnabled);
        });
    }

    /// <summary>
    /// 验证 LoadedPage_ContextReplacementAndUnload_DetachOldModel 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Show、NotifyStateChanged、Equal；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void LoadedPage_ContextReplacementAndUnload_DetachOldModel()
    {
        host.Invoke(() =>
        {
            var first = ContainerLocator.Container.Resolve<ScriptManagerViewModel>();
            var second = ContainerLocator.Container.Resolve<ScriptManagerViewModel>();
            var page = new ScriptManagerView { DataContext = first };
            var window = new Window { Content = page, Width = 800, Height = 500 };
            var firstChanges = 0;
            var secondChanges = 0;
            first.RunAllCommand.CanExecuteChanged += (_, _) => firstChanges++;
            second.RunAllCommand.CanExecuteChanged += (_, _) => secondChanges++;
            try
            {
                window.Show();
                page.DataContext = second;
                firstChanges = secondChanges = 0;
                first.Model.NotifyStateChanged();
                second.Model.NotifyStateChanged();
                Assert.Equal(0, firstChanges);
                Assert.True(secondChanges > 0);
                window.Content = null;
                window.Close();
                page.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                secondChanges = 0;
                second.Model.NotifyStateChanged();
                Assert.Equal(0, secondChanges);
            }
            finally { window.Close(); first.Detach(); second.Detach(); }
        });
    }

    /// <summary>
    /// 验证 RoslynEditor_ThemeChangesPreserveDocument_AndUseWhiteLightSurface 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Invoke、Resolve、Show、Initialize、IsAssignableFrom；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void RoslynEditor_ThemeChangesPreserveDocument_AndUseWhiteLightSurface()
    {
        host.Invoke(() =>
        {
            var theme = ContainerLocator.Container.Resolve<ThemeService>();
            var original = (theme.IsDark, theme.Primary, theme.Secondary);
            var editor = new RoslynCodeEditorView { Text = "// theme check\nvar value = 42;" };
            var window = new Window { Content = editor, Width = 800, Height = 500 };
            try
            {
                window.Show();
                foreach (var dark in new[] { false, true, false })
                {
                    theme.Initialize(dark, original.Primary, original.Secondary);
                    editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var color = Assert.IsAssignableFrom<SolidColorBrush>(editor.Background).Color;
                    Assert.Equal(!dark, color == Colors.White);
                    Assert.Equal(Assert.IsAssignableFrom<SolidColorBrush>(editor.FindResource("MaterialDesign.Brush.Foreground")).Color,
                        Assert.IsAssignableFrom<SolidColorBrush>(editor.Foreground).Color);
                    Assert.Equal("// theme check\nvar value = 42;", editor.Text);
                }
            }
            finally { window.Close(); theme.Initialize(original.IsDark, original.Primary, original.Secondary); }
        });
    }

    /// <summary>
    /// 验证 Button 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Single、OfType、Descendants、Equals；具体条件和先后次序见方法体。
    /// </summary>
    private static Button Button(DependencyObject root, string tooltip) => Descendants(root).OfType<Button>().Single(b => Equals(b.ToolTip, tooltip));
    /// <summary>
    /// 验证 Invoke 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 Button、NotNull、Invoke、GetPattern；具体条件和先后次序见方法体。
    /// </summary>
    private static void Invoke(FrameworkElement root, string tooltip)
    {
        var button = Button(root, tooltip);
        Assert.NotNull(button.Command);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!).Invoke();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    /// <summary>
    /// 验证 Descendants 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 GetChildrenCount、GetChild、Descendants；具体条件和先后次序见方法体。
    /// </summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
