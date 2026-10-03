// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：RegisterNavigationViewEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Prism.Ioc、Prism.Mvvm、System、System.Linq、System.Reflection、System.Windows。导入不代表所有类型都会在运行时加载。
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace Extension.Integration.Prism
{
    /// <summary>
    /// Prism 导航视图注册扩展。
    /// 将导航视图注册为 (object, 导航名) 单例，使 App 层 PrewarmNavigationViewsAsync 预热的实例
    /// 就是导航首次进入时解析到的同一个实例。
    /// （Prism 的 RegisterForNavigation 在 DryIoc 下是瞬时注册：Resolve&lt;object&gt;(name) 每次新建，
    /// 预热实例会被丢弃、导航首进仍要重建，导致预热无效。）
    /// </summary>
    public static class RegisterNavigationViewEx
    {
        /// <summary>
        /// 注册导航视图为 (object, 导航名) 单例。用于视图 XAML 已有 AutoWireViewModel 或无需 VM 的情况。
        /// 导航名默认取类型名（与 RegisterForNavigation 默认一致，需与 RequestNavigate 的名字对应）。
        /// </summary>
        public static IContainerRegistry RegisterNavigationView<TView>(
            this IContainerRegistry registry, string? name = null)
            where TView : FrameworkElement
        {
            var navigationName = name ?? typeof(TView).Name;
            if (!registry.IsRegistered(typeof(object), navigationName))
                registry.RegisterSingleton(typeof(object), typeof(TView), navigationName);
            return registry;
        }

        /// <summary>
        /// 注册导航视图为 (object, 导航名) 单例，并绑定 View→ViewModel。
        /// 等价于 RegisterForNavigation&lt;TView, TViewModel&gt; 的绑定（ViewModelLocationProvider.Register），
        /// 但导航注册为单例。绑定由导航时 RegionNavigationContentLoader 的 AutowireViewModel 触发
        /// （视图 DataContext 已存在则自动跳过）。
        /// </summary>
        public static IContainerRegistry RegisterNavigationView<TView, TViewModel>(
            this IContainerRegistry registry, string? name = null)
            where TView : FrameworkElement
        {
            var navigationName = name ?? typeof(TView).Name;
            if (registry.IsRegistered(typeof(object), navigationName)) return registry;
            registry.RegisterSingleton(typeof(object), typeof(TView), navigationName);
            ViewModelLocationProvider.Register<TView, TViewModel>();
            return registry;
        }

        /// <summary>
        /// 反射批量注册导航视图为 (object, 导航名) 单例（供模块按类型注册，如
        /// ScriptEditorModule / ControlToolModule 的 RegisterViewsByReflection）。
        /// </summary>
        public static IContainerRegistry RegisterNavigationView(
            this IContainerRegistry registry, Type viewType, string? name = null)
        {
            var navigationName = name ?? viewType.Name;
            if (!registry.IsRegistered(typeof(object), navigationName))
                registry.RegisterSingleton(typeof(object), viewType, navigationName);
            return registry;
        }

        /// <summary>
        /// 从指定命名空间反射批量注册导航视图：(object, 导航名) 单例导航注册；预热和导航共用此入口。
        /// 供各 Prism 模块统一调用，替代每个模块各自重复实现的 RegisterViewsByReflection。
        /// </summary>
        /// <param name="registry">容器注册器</param>
        /// <param name="assembly">要扫描的程序集（模块内传 Assembly.GetExecutingAssembly()）</param>
        /// <param name="namespacePrefix">命名空间前缀</param>
        /// <param name="predicate">额外过滤条件（如排除指定类型：t =&gt; t != typeof(UserInfoView)）</param>
        public static IContainerRegistry RegisterNavigationViewsFromNamespace(
            this IContainerRegistry registry,
            Assembly assembly,
            string namespacePrefix,
            Func<Type, bool>? predicate = null)
        {
            var viewTypes = assembly.GetTypes()
                .Where(t => t.Namespace != null &&
                           t.Namespace.StartsWith(namespacePrefix) &&
                           t.IsSubclassOf(typeof(UserControl)) &&
                           t.IsClass &&
                           !t.IsAbstract &&
                           (predicate == null || predicate(t)));

            foreach (var viewType in viewTypes)
            {

                if (!registry.IsRegistered(typeof(object), viewType.Name))
                    registry.RegisterSingleton(typeof(object), viewType, viewType.Name);
            }

            return registry;
        }
    }
}
