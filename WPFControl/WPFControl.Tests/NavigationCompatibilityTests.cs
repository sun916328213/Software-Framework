using System;
using Device.Controls.ViewModels;
using DryIoc;
using Prism.DryIoc;
using Prism.Ioc;
using Xunit;

namespace WPFControl.Tests
{
    /// <summary>
    /// 导航兼容守卫（P8 计划 P8-5 第 14 条）：<see cref="WPFControlModule"/> 完成注册后，
    /// 新 Device 栈的 5 个设备测试页必须已按导航名登记在容器里。
    ///
    /// 为什么需要它：导航注册是**字符串键**，写错或漏注册不会编译报错，
    /// 只会在运行时表现为「点了菜单没反应」；而宿主 Packing Machine 的
    /// PrewarmNavigationViews 会在启动时按名字预热，解析失败会直接打断启动流程。
    ///
    /// 断言里额外钉住「实现类型来自 Device.Controls 程序集」—— 防止日后有人把导航名
    /// 悄悄接回旧 WPFControl 页面，让 P8 的迁移成果静默失效。
    ///
    /// ⚠️ 刻意**不构造页面实例**：新页面的 XAML 依赖宿主 App.xaml 提供的资源键
    /// （如 <c>StaticResource Border_Style</c>），构造它需要一整套宿主资源 + 常驻 STA
    /// Dispatcher 环境 —— 那是集成测试的范畴，塞进单元测试只会让用例随环境漂移
    /// （实测：直接构造会抛 XamlParseException，且共享 Dispatcher 的方案会让测试进程挂住）。
    /// 这里改为查 DryIoc 的注册表：导航键是否存在、实现类型是谁，一次问清。
    /// </summary>
    public sealed class NavigationCompatibilityTests
    {
        /// <summary>新 Device 栈页面的导航名（用于 <see cref="MemberDataAttribute"/>）。</summary>
        /// <returns>导航名清单。</returns>
        public static TheoryData<string> NewDeviceNavigationKeys() => new()
        {
            "CameraView",
            "LightView",
            "RfidView",
            "ScannerView",
            "LabelPrintView",
        };

        [Theory]
        [MemberData(nameof(NewDeviceNavigationKeys))]
        public void NewDeviceNavigationKeys_AreRegisteredToDeviceControlsViews(string navigationName)
        {
            var container = CreateContainer();

            var registration = Assert.Single(
                container.Instance.GetServiceRegistrations(),
                r => r.ServiceType == typeof(object) && Equals(r.OptionalServiceKey, navigationName));

            Assert.NotNull(registration.ImplementationType);
            Assert.Equal(
                "Device.Controls",
                registration.ImplementationType!.Assembly.GetName().Name);
        }

        [Theory]
        [InlineData(typeof(CameraViewModel))]
        [InlineData(typeof(LightViewModel))]
        [InlineData(typeof(RfidViewModel))]
        [InlineData(typeof(ScannerViewModel))]
        [InlineData(typeof(LabelPrintViewModel))]
        public void NewDeviceViewModels_AreResolvableFromContainer(Type viewModelType)
        {
            // ViewModel 依赖各自的 Workspace；Workspace 又依赖工厂。
            // 这条断言守住「工厂 + Workspace + VM」这条注册链是否完整
            // （缺任一环，导航时 AutoWireViewModel 会抛 ContainerResolutionException）。
            var container = CreateContainer();

            Assert.NotNull(container.Resolve(viewModelType));
        }

        /// <summary>
        /// 用真实模块注册流程搭一个最小 Prism 容器。
        /// 只调 <c>RegisterTypes</c>（注册阶段不构造设备实例、不连接硬件），
        /// 不调 <c>OnInitialized</c>（那里会合并资源字典并触发配置迁移，属宿主启动职责）。
        /// </summary>
        private static DryIocContainerExtension CreateContainer()
        {
            var containerExtension = new DryIocContainerExtension();
            var module = new WPFControlModule();
            module.RegisterTypes(containerExtension);
            return containerExtension;
        }
    }
}
