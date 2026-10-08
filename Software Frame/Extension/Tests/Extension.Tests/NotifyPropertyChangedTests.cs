// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：NotifyPropertyChangedTests、Probe、OverridingProbe。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.ComponentModel、System.Reflection、System.Runtime.CompilerServices、Extension.Core.Notify、Xunit。导入不代表所有类型都会在运行时加载。
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
            /// <summary>
            /// 测试相关入口 Raise；准备条件、协作调用与结果检查以方法正文为准。
            /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
            /// </summary>
            public void Raise(string propertyName) => OnPropertyChanged(propertyName);
        }

        /// <summary>覆写 <c>OnPropertyChanged</c>，验证 virtual 分发与 base 调用链。</summary>
        private sealed class OverridingProbe : NotifyPropertyChanged
        {
            public bool OverrideCalled = false;
            public bool CallBase = true;

            /// <summary>
            /// 测试相关入口 Raise；准备条件、协作调用与结果检查以方法正文为准。
            /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
            /// </summary>
            public void Raise(string propertyName) => OnPropertyChanged(propertyName);

            /// <summary>
            /// 测试相关入口 OnPropertyChanged；准备条件、协作调用与结果检查以方法正文为准。
            /// 正文中的关键协作调用包括 OnPropertyChanged；具体条件和先后次序见方法体。
            /// </summary>
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

        /// <summary>
        /// 测试相关入口 OnPropertyChanged_触发事件_发送者是自身且携带属性名；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、Same、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 OnPropertyChanged_未订阅时不抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Null、Exception、Raise；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void OnPropertyChanged_未订阅时不抛异常()
        {
            var probe = new Probe();

            Assert.Null(Record.Exception(() => probe.Raise("Name")));
        }

        /// <summary>
        /// 测试相关入口 OnPropertyChanged_属性名为null时_原样传给事件参数；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、True、Null；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 多个订阅者_全部按注册顺序收到通知；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 某个订阅者抛异常_排在它后面的订阅者收不到通知；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Throws、Raise、False；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 取消订阅后_不再收到通知；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 OnPropertyChanged_参数是裸string_没有CallerMemberName；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetMethod、Single、GetParameters、Equal、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void OnPropertyChanged_参数是裸string_没有CallerMemberName()
        {
            MethodInfo m = typeof(NotifyPropertyChanged)
                .GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            ParameterInfo p = Assert.Single(m.GetParameters());

            Assert.Equal(typeof(string), p.ParameterType);
            Assert.False(p.IsDefined(typeof(CallerMemberNameAttribute), inherit: false));
        }

        /// <summary>
        /// 测试相关入口 覆写OnPropertyChanged后_调用被派发到覆写实现；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、True、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 覆写时若不调base_事件不会触发；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Raise、True、Equal；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 测试相关入口 PropertyChanged是公开实例事件_类型为PropertyChangedEventHandler；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetEvent、NotNull、Equal、False、GetAddMethod；具体条件和先后次序见方法体。
        /// </summary>
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
