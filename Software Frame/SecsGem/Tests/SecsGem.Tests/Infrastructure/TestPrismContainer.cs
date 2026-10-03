// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TestPrismContainer、Entry。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Tests、Prism.Ioc、System、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using SecsGem.Tests;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Tests.Infrastructure
{
    /// <summary>
    /// 测试用迷你 IoC 容器：实现 Prism 的 IContainerProvider/IContainerRegistry。
    /// 支持单例注册（类型/实例/工厂）、构造函数注入解析；仅覆盖 Secs 项目用到的成员。
    /// </summary>
    public sealed class TestPrismContainer : IContainerRegistry, IContainerProvider
    {
        /// <summary>
        /// Entry 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
        /// </summary>
        private sealed class Entry
        {
            public Func<IContainerProvider, object> Factory;
            public object Instance;
        }

        private readonly Dictionary<(Type, string), Entry> _entries =
            new Dictionary<(Type, string), Entry>();

        private readonly object _lock = new object();

        // ── IContainerRegistry ──

        /// <summary>
        /// 验证 RegisterInstance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterInstance；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterInstance(Type type, object instance)
            => RegisterInstance(type, instance, string.Empty);

        /// <summary>
        /// 验证 RegisterInstance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Set；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterInstance(Type type, object instance, string name)
        {
            Set(type, name, _ => instance);
            return this;
        }

        /// <summary>
        /// 验证 RegisterSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterSingleton、CreateInstance；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterSingleton(Type type, Type implementingType)
            => RegisterSingleton(type, c => CreateInstance(implementingType));

        /// <summary>
        /// 验证 RegisterSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterSingleton、CreateInstance；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterSingleton(Type type, Type implementingType, string name)
            => RegisterSingleton(type, name, c => CreateInstance(implementingType));

        /// <summary>
        /// 验证 RegisterSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterSingleton、factory；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterSingleton(Type type, Func<object> factory)
            => RegisterSingleton(type, _ => factory());

        /// <summary>
        /// 验证 RegisterSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterSingleton；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterSingleton(Type type, Func<IContainerProvider, object> factory)
            => RegisterSingleton(type, string.Empty, factory);

        /// <summary>
        /// 验证 RegisterManySingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 RegisterSingleton；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterManySingleton(Type type, params Type[] implementingTypes)
        {
            foreach (var t in implementingTypes)
            {
                RegisterSingleton(t, type);
            }
            return this;
        }

        /// <summary>
        /// 验证 Register 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register、CreateInstance；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry Register(Type type, Type implementingType)
            => Register(type, c => CreateInstance(implementingType));

        /// <summary>
        /// 验证 Register 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register、CreateInstance；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry Register(Type type, Type implementingType, string name)
            => Register(type, name, c => CreateInstance(implementingType));

        /// <summary>
        /// 验证 Register 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register、factory；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry Register(Type type, Func<object> factory)
            => Register(type, _ => factory());

        /// <summary>
        /// 验证 Register 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry Register(Type type, Func<IContainerProvider, object> factory)
            => Register(type, string.Empty, factory);

        /// <summary>
        /// 验证 RegisterMany 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterMany(Type type, params Type[] implementingTypes)
        {
            foreach (var t in implementingTypes)
            {
                Register(t, type);
            }
            return this;
        }

        /// <summary>
        /// 验证 RegisterScoped 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterScoped(Type type, Type implementingType) => Register(type, implementingType);
        /// <summary>
        /// 验证 RegisterScoped 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterScoped(Type type, Func<object> factory) => Register(type, factory);
        /// <summary>
        /// 验证 RegisterScoped 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Register；具体条件和先后次序见方法体。
        /// </summary>
        public IContainerRegistry RegisterScoped(Type type, Func<IContainerProvider, object> factory) => Register(type, factory);

        /// <summary>
        /// 验证 IsRegistered 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ContainsKey；具体条件和先后次序见方法体。
        /// </summary>
        public bool IsRegistered(Type type) => _entries.ContainsKey((type, string.Empty));
        /// <summary>
        /// 验证 IsRegistered 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ContainsKey；具体条件和先后次序见方法体。
        /// </summary>
        public bool IsRegistered(Type type, string name) => _entries.ContainsKey((type, name));

        // ── IContainerProvider ──

        public IScopedProvider CurrentScope => null;

        /// <summary>
        /// 验证 CreateScope 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// </summary>
        public IScopedProvider CreateScope()
            => throw new NotSupportedException("测试容器不支持 Scope");

        /// <summary>
        /// 验证 Resolve 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public object Resolve(Type type) => Resolve(type, string.Empty);
        /// <summary>
        /// 验证 Resolve 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public object Resolve(Type type, params (Type Type, object Instance)[] parameters)
            => Resolve(type, string.Empty, parameters);
        /// <summary>
        /// 验证 Resolve 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Resolve、Empty；具体条件和先后次序见方法体。
        /// </summary>
        public object Resolve(Type type, string name) => Resolve(type, name, Array.Empty<(Type Type, object Instance)>());

        /// <summary>
        /// 验证 Resolve 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TryGetValue、Factory、CreateInstance；具体条件和先后次序见方法体。
        /// 方法内使用锁保护部分共享操作；锁外操作的并发约束仍需结合调用者核对。
        /// </summary>
        public object Resolve(Type type, string name, params (Type Type, object Instance)[] parameters)
        {
            if (_entries.TryGetValue((type, name ?? string.Empty), out var entry))
            {
                lock (_lock)
                {
                    if (entry.Instance == null)
                    {
                        entry.Instance = entry.Factory(this);
                    }
                }
                return entry.Instance;
            }

            if (type == typeof(IContainerProvider))
            {
                return this;
            }

            // 未注册的类型尝试直接创建（瞬态），便于解析简单依赖
            return CreateInstance(type);
        }

        // ── 私有辅助 ──

        private void Set(Type type, string name, Func<IContainerProvider, object> factory)
        {
            _entries[(type, name ?? string.Empty)] = new Entry { Factory = factory };
        }

        /// <summary>
        /// 验证 Register 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Set；具体条件和先后次序见方法体。
        /// </summary>
        private IContainerRegistry Register(Type type, string name, Func<IContainerProvider, object> factory)
        {
            Set(type, name, factory);
            return this;
        }

        /// <summary>
        /// 验证 RegisterSingleton 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Set；具体条件和先后次序见方法体。
        /// </summary>
        private IContainerRegistry RegisterSingleton(Type type, string name, Func<IContainerProvider, object> factory)
        {
            Set(type, name, factory);
            return this;
        }

        /// <summary>
        /// 验证 CreateInstance 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 First、OrderByDescending、GetConstructors、GetParameters、ToArray；具体条件和先后次序见方法体。
        /// </summary>
        private object CreateInstance(Type type)
        {
            var ctor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var args = ctor.GetParameters()
                .Select(p => p.ParameterType == typeof(IContainerProvider)
                    ? (object)this
                    : Resolve(p.ParameterType))
                .ToArray();

            return Activator.CreateInstance(type, args);
        }
    }
}
