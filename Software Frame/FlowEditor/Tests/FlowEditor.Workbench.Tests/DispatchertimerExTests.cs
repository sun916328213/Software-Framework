// 【中文阅读说明】
// FlowEditor 模块：流程模型、执行和编辑器协作；Core、Runtime 与 Controls 分别承担模型契约、执行能力和界面职责。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：DispatchertimerExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.UI.Controls、Xunit。导入不代表所有类型都会在运行时加载。
using Extension.UI.Controls;
using Xunit;

namespace FlowEditor.Workbench.Tests;

/// <summary>跨线程定时器扩展：无 WPF 宿主（Application.Current == null）下不抛 NRE，命名幂等生效。</summary>
public class DispatchertimerExTests
{
    /// <summary>
    /// 验证 DispatcherTimer_Headless_NoNRE_AndAutoStart 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DispatcherTimer、True、Stop；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void DispatcherTimer_Headless_NoNRE_AndAutoStart()
    {
        Action loop = () => { };
        var timer = loop.DispatcherTimer(1000);

        Assert.True(timer.IsEnabled);
        timer.Stop();
    }

    /// <summary>
    /// 验证 NamedTimer_SameKey_ReplacesInsteadOfStacking 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 DispatcherTimer、True、StopTimer、False；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void NamedTimer_SameKey_ReplacesInsteadOfStacking()
    {
        Action loop = () => { };
        var t1 = loop.DispatcherTimer(1000, key: "test-timer");
        var t2 = loop.DispatcherTimer(1000, key: "test-timer");

        Assert.True(t1.IsEnabled == false, "旧定时器应被替换停止"); // 同 key 替换：旧的 Stop
        Assert.True(t2.IsEnabled);

        Assert.True(DispatcherTimerEx.StopTimer("test-timer"));
        Assert.False(t2.IsEnabled);
    }

    /// <summary>
    /// 验证 StopTimer_MissingKey_ReturnsFalse 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 False、StopTimer；具体条件和先后次序见方法体。
    /// </summary>
    [Fact]
    public void StopTimer_MissingKey_ReturnsFalse()
    {
        Assert.False(DispatcherTimerEx.StopTimer("不存在的key"));
    }
}
