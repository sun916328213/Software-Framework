// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：FlowNodeEditorHostTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Windows、System.Windows.Media、System.Windows.Media.Imaging、System.Windows.Interop、System.Windows.Threading、FlowEditor.Controls.Core。导入不代表所有类型都会在运行时加载。
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using FlowEditor.Controls.Core;
using FlowEditor.Controls.Interfaces;
using FlowEditor.Controls.Presentation.NodeEditing;
using FlowEditor.Controls.Services;
using FlowNodes.Presentation.NodeEditing;
using FlowNodes.Script;
using Prism.Ioc;
using ScriptEditor.Controls.Views;
using Vision.Controls.View;
using Vision.Core.Tools;
using Xunit;

namespace MachineApp.Host.Tests;

/// <summary>
/// FlowNodeEditorHostTests 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
[Collection("ProductionComposition")]
public sealed class FlowNodeEditorHostTests(ProductionFixture fixture)
{
    /// <summary>
    /// 测试相关入口 ProductionRegistersTheExtensionAndRetainsEditorWindowShape；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、EnsureHandle、Contains、Resolve、IsType；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void ProductionRegistersTheExtensionAndRetainsEditorWindowShape()
    {
        fixture.Invoke(container =>
        {
            // 离线宿主不显示壳；建立其 HWND，模拟生产中已显示的 Owner，保持产品代码不变。
            new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
            Assert.Contains(container.Resolve<IEnumerable<INodeEditorHandler>>(), handler => handler is FlowNodesEditorAdapter);
            Assert.IsType<WpfNodeEditorLauncher>(container.Resolve<INodeEditorLauncher>());
            var adapter = new FlowNodesEditorAdapter();
            var script = new ScriptNode { Name = "script", EditorState = new() { Code = "return 42;" } };
            var scriptWindow = adapter.CreateWindow(script);
            try
            {
                Assert.Equal("脚本编辑器 - script", scriptWindow.Title);
                Assert.IsType<ScriptEditorView>(scriptWindow.Content);
                AssertWindowShape(scriptWindow);
            }
            finally { scriptWindow.Close(); }

            var vision = new FlowNodeSeamTests.RecordingVisionNode { Name = "vision" };
            Window? visionWindow = null;
            try
            {
                visionWindow = adapter.CreateWindow(vision);
                Assert.Equal("视觉工具 - vision", visionWindow.Title);
                var editor = Assert.IsType<ToolEditorView>(visionWindow.Content);
                Assert.Same(vision.visionTool, editor.Tool);
                AssertWindowShape(visionWindow);
                editor.Measure(new Size(900, 700));
                editor.Arrange(new Rect(0, 0, 900, 700));
                editor.UpdateLayout();
                var bitmap = new RenderTargetBitmap(900, 700, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(editor);
                Assert.Equal(900, bitmap.PixelWidth);
            }
            finally { visionWindow?.Close(); ToolMge.Instance.Remove(vision.visionTool); vision.visionTool.Dispose(); }
        });
    }

    /// <summary>
    /// 测试相关入口 ProductionLauncherOpensAndClosesTheActualVisualEditor；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、EnsureHandle、FromMilliseconds、FirstOrDefault、OfType；具体条件和先后次序见方法体。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Fact]
    public void ProductionLauncherOpensAndClosesTheActualVisualEditor()
    {
        fixture.Invoke(container =>
        {
            new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
            var node = new FlowNodeSeamTests.RecordingVisionNode { Name = "S1-modal-vision" };
            Window? dialog = null;
            Exception? observationError = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.Title == "视觉工具 - S1-modal-vision" && window.IsVisible);
                if (dialog is null) return;
                timer.Stop();
                try { Assert.IsType<ToolEditorView>(dialog.Content); AssertWindowShape(dialog); }
                catch (Exception error) { observationError = error; }
                finally { dialog.Close(); }
            };
            try
            {
                node.OnAttached(container.Resolve<FlowChartService>());
                timer.Start();
                node.EditScript();
                Assert.NotNull(dialog);
                Assert.Null(observationError);
            }
            finally { timer.Stop(); dialog?.Close(); node.OnDetached(); node.visionTool.Dispose(); }
        });
    }

    /// <summary>
    /// 测试相关入口 ScriptEditorClosesAndWritesBackThroughTheProductionNodeAction；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Invoke、EnsureHandle、IsType、Create、Resolve；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
    /// </summary>
    [Fact]
    public void ScriptEditorClosesAndWritesBackThroughTheProductionNodeAction()
    {
        fixture.Invoke(container =>
        {
            new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
            var node = Assert.IsType<ScriptNode>(container.Resolve<INodeFactory>().Create(typeof(ScriptNode)));
            node.Name = "S1-modal-script";
            node.EditorState.Code = "return 42;";
            node.OnAttached(container.Resolve<FlowChartService>());
            Window? dialog = null;
            Exception? observationError = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += async (_, _) =>
            {
                dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.Title == "脚本编辑器 - S1-modal-script" && window.IsVisible);
                if (dialog is null) return;
                timer.Stop();
                try
                {
                    var editor = Assert.IsType<ScriptEditorView>(dialog.Content);
                    Assert.True(await editor.EnsureInitializedAsync());
                    Assert.Equal("return 42;", editor.Document.Code);
                    editor.SetScriptCode("return 43;");
                    AssertWindowShape(dialog);
                }
                catch (Exception error) { observationError = error; }
                finally { dialog.Close(); }
            };
            try
            {
                timer.Start();
                node.EditScript();
                Assert.NotNull(dialog);
                Assert.Null(observationError);
                Assert.Equal("return 43;", node.EditorState.Code);
            }
            finally { timer.Stop(); dialog?.Close(); node.OnDetached(); }
        });
    }

    /// <summary>
    /// 测试相关入口 AssertWindowShape；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 Equal、Same；具体条件和先后次序见方法体。
    /// </summary>
    private static void AssertWindowShape(Window window)
    {
        Assert.Equal(900, window.Width);
        Assert.Equal(700, window.Height);
        Assert.Same(Application.Current.MainWindow, window.Owner);
        Assert.Equal(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
    }
}
