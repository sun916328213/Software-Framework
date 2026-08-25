using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.ReflectionEx
{
    public static class RegisterServicesEx
    {
        /// <summary>
        /// 从指定命名空间注册单例服务（仅注册类，不注册方法）
        /// </summary>
        /// <param name="assembly">Assembly.GetExecutingAssembly();</param>
        /// <param name="containerRegistry">容器注册器</param>
        /// <param name="namespaceName">命名空间前缀</param>
        public static void RegisterSingletonFromNamespace(Assembly assembly, IContainerRegistry containerRegistry, string namespaceName)
        {
            var serviceTypes = assembly.GetTypes()
                .Where(t => t.Namespace != null &&
                           t.Namespace.StartsWith(namespaceName) &&
                           t.IsClass &&
                           !t.IsAbstract &&
                           // 确保只注册类，不注册委托或其他成员
                           t.MemberType == MemberTypes.TypeInfo);

            foreach (var type in serviceTypes)
            {
                // 只注册类本身作为单例
                containerRegistry.RegisterSingleton(type);
            }
        }

        /// <summary>
        /// 从指定命名空间反射批量注册单例服务（扩展方法形式，供各模块统一调用，
        /// 替代每个模块各自重复实现的 RegisterServicesByReflection）。
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        /// <param name="assembly">要扫描的程序集（模块内传 Assembly.GetExecutingAssembly()）</param>
        /// <param name="namespacePrefix">命名空间前缀</param>
        /// <param name="predicate">额外过滤条件</param>
        public static IContainerRegistry RegisterServicesFromNamespace(
            this IContainerRegistry containerRegistry,
            Assembly assembly,
            string namespacePrefix,
            Func<Type, bool>? predicate = null)
        {
            var serviceTypes = assembly.GetTypes()
                .Where(t => t.Namespace != null &&
                           t.Namespace.StartsWith(namespacePrefix) &&
                           t.IsClass &&
                           !t.IsAbstract &&
                           t.MemberType == MemberTypes.TypeInfo &&
                           (predicate == null || predicate(t)));

            foreach (var type in serviceTypes)
            {
                containerRegistry.RegisterSingleton(type);
            }

            return containerRegistry;
        }
    }
}