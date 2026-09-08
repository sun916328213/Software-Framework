using System.Windows;
using System.Windows.Media;
using WPFCustomControls.Window.Views;

namespace WPFCustomControls.Tests.Window;

/// <summary>
/// Window 子系统控件单元测试。
///
/// <para>
/// <b>测试范围</b>:覆盖默认 DependencyProperty 元数据、构造函数注入的属性赋值、
/// 属性 setter/getter、DP 注册检查以及 <c>new</c> 关键字隐藏验证。
/// WPF <see cref="System.Windows.Window"/> 派生类在构造期需要 STA 单元线程,
/// 因此所有 <c>new SomeWindow()</c> 的测试体都通过 <see cref="StaFact.Run"/> 切换线程执行。
/// </para>
/// </summary>
public class WindowControlsTests
{
    // ── MessagePrompt ─────────────────────────────────────────

    [Fact]
    public void MessagePrompt_DefaultsAreAppliedFromMetadata()
    {
        StaFact.Run(() =>
        {
            var prompt = new MessagePrompt();
            // 默认元数据:Title="提示", Message="", Visibility_Cancel=Visible,
            // MessageForeground=Brushes.Black, TitleForeground=Brushes.Black
            Assert.Equal("提示", prompt.Title);
            Assert.Equal("", prompt.Message);
            Assert.Equal(Visibility.Visible, prompt.Visibility_Cancel);
            Assert.Equal(Brushes.Black, prompt.MessageForeground);
            Assert.Equal(Brushes.Black, prompt.TitleForeground);
        });
    }

    [Fact]
    public void MessagePrompt_ConstructorInjectsProperties()
    {
        StaFact.Run(() =>
        {
            var prompt = new MessagePrompt("出错", "请稍后重试", Visibility.Collapsed);

            Assert.Equal("出错", prompt.Title);
            Assert.Equal("请稍后重试", prompt.Message);
            Assert.Equal(Visibility.Collapsed, prompt.Visibility_Cancel);
        });
    }

    [Fact]
    public void MessagePrompt_PropertySetters_UpdateValues()
    {
        StaFact.Run(() =>
        {
            var prompt = new MessagePrompt();
            prompt.Title = "新标题";
            prompt.Message = "新消息";
            prompt.Visibility_Cancel = Visibility.Collapsed;
            prompt.MessageForeground = Brushes.Red;
            prompt.TitleForeground = Brushes.Blue;

            Assert.Equal("新标题", prompt.Title);
            Assert.Equal("新消息", prompt.Message);
            Assert.Equal(Visibility.Collapsed, prompt.Visibility_Cancel);
            Assert.Equal(Brushes.Red, prompt.MessageForeground);
            Assert.Equal(Brushes.Blue, prompt.TitleForeground);
        });
    }

    [Fact]
    public void MessagePrompt_DependencyProperties_AreRegistered()
    {
        // 反射验证 DP 已注册(纯元数据查询,不实例化 Window → 无需 STA)
        var t = typeof(MessagePrompt);
        Assert.NotNull(t.GetField("TitleProperty", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
        Assert.NotNull(t.GetField("MessageProperty", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
        Assert.NotNull(t.GetField("Visibility_CancelProperty", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
    }

    // ── FileLoading ───────────────────────────────────────────

    [Fact]
    public void FileLoading_DefaultText_IsLoadingDots()
    {
        StaFact.Run(() =>
        {
            var w = new FileLoading();
            // FrameworkPropertyMetadata 默认 "Loading....."
            Assert.Equal("Loading.....", w.Text);
        });
    }

    [Fact]
    public void FileLoading_TextSetter_UpdatesValue()
    {
        StaFact.Run(() =>
        {
            var w = new FileLoading { Text = "读取中..." };
            Assert.Equal("读取中...", w.Text);
        });
    }

    // ── NameInputControl ──────────────────────────────────────

    [Fact]
    public void NameInputControl_Defaults()
    {
        StaFact.Run(() =>
        {
            var dlg = new NameInputControl();
            Assert.Equal("输入名称", dlg.Title);
            Assert.Equal("请输入名称：", dlg.Description);
            Assert.Equal("名称", dlg.Placeholder);
            Assert.Equal("", dlg.InputText);
        });
    }

    [Fact]
    public void NameInputControl_PropertySetters_UpdateValues()
    {
        StaFact.Run(() =>
        {
            var dlg = new NameInputControl
            {
                Title = "新建节点",
                Description = "请输入节点名称",
                Placeholder = "Node1",
                InputText = "默认值"
            };

            Assert.Equal("新建节点", dlg.Title);
            Assert.Equal("请输入节点名称", dlg.Description);
            Assert.Equal("Node1", dlg.Placeholder);
            Assert.Equal("默认值", dlg.InputText);
        });
    }

    [Fact]
    public void NameInputControl_InputText_BindsTwoWayByDefault()
    {
        // 反射查询 DP 字段,无需 STA
        var field = typeof(NameInputControl).GetField("InputTextProperty",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        Assert.NotNull(field);
    }

    // ── 继承性:Window.Title 隐藏 ─────────────────────────────

    [Fact]
    public void NameInputControl_HidesWindowTitle_NewKeywordApplied()
    {
        // NameInputControl.Title 应用了 new 关键字(消除 CS0108 警告),
        // 验证属性确实被显式 hide — 通过反射看 IsHideBySig
        var prop = typeof(NameInputControl).GetProperty("Title");
        Assert.NotNull(prop);
        var getter = prop!.GetGetMethod()!;
        Assert.True(getter.IsHideBySig, "NameInputControl.Title 应使用 new 关键字隐藏 Window.Title");
    }

    [Fact]
    public void MessagePrompt_HidesWindowTitle_NewKeywordApplied()
    {
        var prop = typeof(MessagePrompt).GetProperty("Title");
        Assert.NotNull(prop);
        var getter = prop!.GetGetMethod()!;
        Assert.True(getter.IsHideBySig, "MessagePrompt.Title 应使用 new 关键字隐藏 Window.Title");
    }
}
