using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Runtime.Tests.Infrastructure
{
    /// <summary>
    /// 测试用迷你 IoC 容器：实现 Prism 的 IContainerProvider/IContainerRegistry。
    /// 支持单例注册（类型/实例/工厂）、构造函数注入解析；仅覆盖 Secs 项目用到的成员。
    /// </summary>
    public sealed class TestPrismContainer : IContainerRegistry, IContainerProvider
    {
        private sealed class Entry
        {
            public Func<IContainerProvider, object> Factory;
            public object Instance;
        }

        private readonly Dictionary<(Type, string), Entry> _entries =
            new Dictionary<(Type, string), Entry>();

        private readonly object _lock = new object();

        // ── IContainerRegistry ──

        public IContainerRegistry RegisterInstance(Type type, object instance)
            => RegisterInstance(type, instance, string.Empty);

        public IContainerRegistry RegisterInstance(Type type, object instance, string name)
        {
            Set(type, name, _ => instance);
            return this;
        }

        public IContainerRegistry RegisterSingleton(Type type, Type implementingType)
            => RegisterSingleton(type, c => CreateInstance(implementingType));

        public IContainerRegistry RegisterSingleton(Type type, Type implementingType, string name)
            => RegisterSingleton(type, name, c => CreateInstance(implementingType));

        public IContainerRegistry RegisterSingleton(Type type, Func<object> factory)
            => RegisterSingleton(type, _ => factory());

        public IContainerRegistry RegisterSingleton(Type type, Func<IContainerProvider, object> factory)
            => RegisterSingleton(type, string.Empty, factory);

        public IContainerRegistry RegisterManySingleton(Type type, params Type[] implementingTypes)
        {
            foreach (var t in implementingTypes)
            {
                RegisterSingleton(t, type);
            }
            return this;
        }

        public IContainerRegistry Register(Type type, Type implementingType)
            => Register(type, c => CreateInstance(implementingType));

        public IContainerRegistry Register(Type type, Type implementingType, string name)
            => Register(type, name, c => CreateInstance(implementingType));

        public IContainerRegistry Register(Type type, Func<object> factory)
            => Register(type, _ => factory());

        public IContainerRegistry Register(Type type, Func<IContainerProvider, object> factory)
            => Register(type, string.Empty, factory);

        public IContainerRegistry RegisterMany(Type type, params Type[] implementingTypes)
        {
            foreach (var t in implementingTypes)
            {
                Register(t, type);
            }
            return this;
        }

        public IContainerRegistry RegisterScoped(Type type, Type implementingType) => Register(type, implementingType);
        public IContainerRegistry RegisterScoped(Type type, Func<object> factory) => Register(type, factory);
        public IContainerRegistry RegisterScoped(Type type, Func<IContainerProvider, object> factory) => Register(type, factory);

        public bool IsRegistered(Type type) => _entries.ContainsKey((type, string.Empty));
        public bool IsRegistered(Type type, string name) => _entries.ContainsKey((type, name));

        // ── IContainerProvider ──

        public IScopedProvider CurrentScope => null;

        public IScopedProvider CreateScope()
            => throw new NotSupportedException("测试容器不支持 Scope");

        public object Resolve(Type type) => Resolve(type, string.Empty);
        public object Resolve(Type type, params (Type Type, object Instance)[] parameters)
            => Resolve(type, string.Empty, parameters);
        public object Resolve(Type type, string name) => Resolve(type, name, Array.Empty<(Type Type, object Instance)>());

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

        private IContainerRegistry Register(Type type, string name, Func<IContainerProvider, object> factory)
        {
            Set(type, name, factory);
            return this;
        }

        private IContainerRegistry RegisterSingleton(Type type, string name, Func<IContainerProvider, object> factory)
        {
            Set(type, name, factory);
            return this;
        }

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
