using DataManager.SQL.Definition;
using System;
using System.Collections.Generic;

namespace DataManager.SQL
{
    /// <summary>
    /// SQL 管理器（已去 Prism 容器依赖，P0-2）。
    /// 需要注册到 IOC 时，由上层（DataManagerModule/Workbench）显式注册，不再由 Manager 隐式操作容器。
    /// P12：实例创建经 <see cref="ISqlProviderFactory"/> 可替换接缝；无参构造默认使用 Runtime 内建工厂，
    /// 旧调用 <c>new SQLManager()</c> 与 <c>Create&lt;T&gt;(SQLParam)</c> 签名行为保持。
    /// </summary>
    public class SQLManager : ManagerBase<ISQL, SQLParam>, ISQLManager
    {
        private readonly Action<object>? _onCreated;
        private readonly ISqlProviderFactory _factory;

        public SQLManager()
            : this(new SqlProviderFactory(), null)
        {
        }

        /// <summary>
        /// 可选：创建实例后的回调（上层可用于 RegisterInstance 到容器）。
        /// </summary>
        public SQLManager(Action<object> onCreated)
            : this(new SqlProviderFactory(), onCreated)
        {
        }

        /// <summary>
        /// 使用可注入的 Provider 工厂创建管理器；测试可传入 Fake/Stub，无需真实 Provider。
        /// </summary>
        public SQLManager(ISqlProviderFactory factory, Action<object>? onCreated = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _onCreated = onCreated;
        }
 
        public override void Config(List<SQLParam> param)
        {
            foreach (var item in param)
                Create(item);
        }

        public ISQL<T> Create<T>(SQLParam param, Action<ISQL<T>>? onCreated = null) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(param);
            param.Validate();
            if (Contains(param.Name))
                throw new InvalidOperationException($"已存在同名数据库实例: {param.Name}");
            ISQL<T> sql = _factory.Create<T>(param);

            Add(sql);
            _onCreated?.Invoke(sql);
            onCreated?.Invoke(sql);

            return sql;
        }

        public override ISQL Create(SQLParam param)
        {
            throw new NotSupportedException("Please use Create<T> method instead.");
        }

        protected override string GetName(ISQL device)
        {
            return device.SQLParam.Name;
        }
    }
}
