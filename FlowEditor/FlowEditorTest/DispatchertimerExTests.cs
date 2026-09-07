using Extension.Core.ControlEx;
using Xunit;

namespace FlowEditorTest;

/// <summary>跨线程定时器扩展：无 WPF 宿主（Application.Current == null）下不抛 NRE，命名幂等生效。</summary>
public class DispatchertimerExTests
{
    [Fact]
    public void DispatcherTimer_Headless_NoNRE_AndAutoStart()
    {
        Action loop = () => { };
        var timer = loop.DispatcherTimer(1000);

        Assert.True(timer.IsEnabled);
        timer.Stop();
    }

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

    [Fact]
    public void StopTimer_MissingKey_ReturnsFalse()
    {
        Assert.False(DispatcherTimerEx.StopTimer("不存在的key"));
    }
}
