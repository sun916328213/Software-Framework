// 【中文阅读说明】
// WPFCustomControls 模块：基础 WPF 控件、属性编辑器及主题资源，供业务界面与工作台复用。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：StaFact。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFCustomControls、System、System.Threading、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using WPFCustomControls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WPFCustomControls.Tests.Window;

/// <summary>
/// STA 线程切换工具。
///
/// <para>
/// WPF Window/FrameworkElement 构造时需要 STA 单元线程,xUnit 默认 MTA。
/// 本类提供 <see cref="Run"/> 把测试体放到 STA 线程上执行,并把异常/失败抛回主线程。
/// </para>
///
/// <para>
/// <b>使用</b>:STA 测试方法体用 <c>StaFact.Run(() =&gt; { ... });</c> 包住逻辑。
/// </para>
///
/// <example>
/// <code>
/// [Fact]
/// public void NewWindow_DoesNotThrow()
/// {
///     StaFact.Run(() =&gt;
///     {
///         var w = new MyWindow();
///         Assert.NotNull(w);
///     });
/// }
/// </code>
/// </example>
/// </summary>
internal static class StaFact
{
    /// <summary>
    /// 在 STA 线程上同步执行 <paramref name="action"/>,把异常/Assert 失败抛回调用线程。
    /// </summary>
    public static void Run(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                TestApplicationResources.EnsureInitialized();
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
            throw captured;
    }
}
