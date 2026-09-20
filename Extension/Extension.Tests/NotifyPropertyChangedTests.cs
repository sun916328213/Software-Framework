using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Extension.Core.Notify;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「变更通知基类」专项：<c>Extension.Core.Notify.NotifyPropertyChanged</c>（14 行）。
    /// <para>
    /// 背景：该类型**全仓 0 引用**（硬规则 1 ⇒ 不删，只隔离在 <c>Core/Notify/</c>）。
    /// 但它属于 T0 的公开 API 面，未来 ViewModel 可能继承它，所以这里钉住的是
    /// **契约本身**（而不是调用方行为）：
    /// <list type="number">
    /// <item><c>OnPropertyChanged</c> 是 <c>protected virtual</c> ⇒ 派生类可覆写；
    /// 覆写后**必须自己调 <c>base</c>**，否则事件不触发（基类不做任何兜底）；</item>
    /// <item>参数是裸 <c>string</c>，**没有 <c>[CallerMemberName]</c>**
    /// ⇒ 调用方必须显式传属性名，且 <c>null</c> 不会被自动补成调用者名字；</item>
    /// <item>事件用 <c>PropertyChanged?.Invoke</c> 触发 ⇒ 走**多播委托默认语义**：
    /// 某个订阅者抛异常时，**排在它后面的订阅者不会被调用**（不是"逐个 try"）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class NotifyPropertyChangedTests
    {
        /// <summary>把 <c>protected</c> 的 <c>OnPropertyChanged</c> 暴露出来。</summary>
        private sealed class Probe : NotifyPropertyChanged
        {
            public void Raise(string propertyName) => OnPropertyChanged(propertyName);
        }

        /// <summary>覆写 <c>OnPropertyChanged</c>，验证 virtual 分发与 base 调用链。</summary>
        private sealed class OverridingProbe : NotifyPropertyChanged
        {
            public bool OverrideCalled = false;
            public bool CallBase = true;

            public void Raise(string propertyName) => OnPropertyChanged(propertyName);

            protected override void OnPropertyChanged(string propertyName)
            {
                OverrideCalled = true;
                if (CallBase)
                {
                    base.OnPropertyChanged(propertyName);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 事件契约
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 实现INotifyPropertyChanged接口()
        {
            Assert.True(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(NotifyPropertyChanged)));
        }

        [Fact]
        public void OnPropertyChanged_触发事件_发送者是自身且携带属性名()
        {
            var probe = new Probe();
            object sender = null;
            string name = null;
            probe.PropertyChanged += (s, e) => { sender = s; name = e.PropertyName; };

            probe.Raise("Name");

            Assert.Same(probe, sender);
            Assert.Equal("Name", name);
        }

        [Fact]
        public void OnPropertyChanged_未订阅时不抛异常()
        {
            var probe = new Probe();

            Assert.Null(Record.Exception(() => probe.Raise("Name")));
        }

        [Fact]
        public void OnPropertyChanged_属性名为null时_原样传给事件参数()
        {
            // 没有 [CallerMemberName] 兜底 ⇒ null 就是 null，不会被补成调用者名字
            var probe = new Probe();
            bool raised = false;
            string name = "sentinel";
            probe.PropertyChanged += (s, e) => { raised = true; name = e.PropertyName; };

            probe.Raise(null);

            Assert.True(raised);
            Assert.Null(name);
        }

        [Fact]
        public void 多个订阅者_全部按注册顺序收到通知()
        {
            var probe = new Probe();
            string order = string.Empty;
            probe.PropertyChanged += (s, e) => order += "A";
            probe.PropertyChanged += (s, e) => order += "B";
            probe.PropertyChanged += (s, e) => order += "C";

            probe.Raise("X");

            Assert.Equal("ABC", order);
        }

        [Fact]
        public void 某个订阅者抛异常_排在它后面的订阅者收不到通知()
        {
            // PropertyChanged?.Invoke(...) 走多播委托默认语义：异常直接冒泡、后续处理器被跳过。
            // 谁把这里改成"逐个 try/catch 隔离"，这条用例就会变红 —— 那正是它的作用。
            var probe = new Probe();
            bool secondCalled = false;
            probe.PropertyChanged += (s, e) => throw new InvalidOperationException("boom");
            probe.PropertyChanged += (s, e) => secondCalled = true;

            Assert.Throws<InvalidOperationException>(() => probe.Raise("X"));
            Assert.False(secondCalled);
        }

        [Fact]
        public void 取消订阅后_不再收到通知()
        {
            var probe = new Probe();
            int count = 0;
            PropertyChangedEventHandler handler = (s, e) => count++;
            probe.PropertyChanged += handler;

            probe.Raise("A");
            probe.PropertyChanged -= handler;
            probe.Raise("B");

            Assert.Equal(1, count);
        }

        // ══════════════════════════════════════════════════════════════════
        // 继承契约
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void OnPropertyChanged_是protected_virtual_可覆写()
        {
            MethodInfo m = typeof(NotifyPropertyChanged)
                .GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(m);
            Assert.True(m.IsFamily, "应为 protected");
            Assert.True(m.IsVirtual && !m.IsFinal, "应可覆写（排除编译器补的 virtual+final 接口槽）");
            Assert.Equal(typeof(void), m.ReturnType);
        }

        [Fact]
        public void OnPropertyChanged_参数是裸string_没有CallerMemberName()
        {
            MethodInfo m = typeof(NotifyPropertyChanged)
                .GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterInfo p = Assert.Single(m.GetParameters());

            Assert.Equal(typeof(string), p.ParameterType);
            Assert.False(p.IsDefined(typeof(CallerMemberNameAttribute), inherit: false));
        }

        [Fact]
        public void 覆写OnPropertyChanged后_调用被派发到覆写实现()
        {
            var probe = new OverridingProbe();
            int raised = 0;
            probe.PropertyChanged += (s, e) => raised++;

            probe.Raise("Name");

            Assert.True(probe.OverrideCalled);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void 覆写时若不调base_事件不会触发()
        {
            // 基类不做兜底：覆写方漏掉 base.OnPropertyChanged 就是彻底静默（不报错、不通知）
            var probe = new OverridingProbe { CallBase = false };
            int raised = 0;
            probe.PropertyChanged += (s, e) => raised++;

            probe.Raise("Name");

            Assert.True(probe.OverrideCalled);
            Assert.Equal(0, raised);
        }

        // ══════════════════════════════════════════════════════════════════
        // 类型形状
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 类型是普通class_非sealed_有公开无参构造()
        {
            Type t = typeof(NotifyPropertyChanged);

            Assert.False(t.IsAbstract);
            Assert.False(t.IsSealed);
            Assert.NotNull(t.GetConstructor(Type.EmptyTypes));
            Assert.Equal("Extension.Core.Notify", t.Namespace);
        }

        [Fact]
        public void PropertyChanged是公开实例事件_类型为PropertyChangedEventHandler()
        {
            EventInfo e = typeof(NotifyPropertyChanged)
                .GetEvent("PropertyChanged", BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(e);
            Assert.Equal(typeof(PropertyChangedEventHandler), e.EventHandlerType);
            Assert.Equal(typeof(NotifyPropertyChanged), e.DeclaringType);
            Assert.False(e.GetAddMethod().IsStatic);
        }
    }
}
