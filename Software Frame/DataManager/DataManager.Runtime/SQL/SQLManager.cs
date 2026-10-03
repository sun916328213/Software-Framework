// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：SQLManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.SQL、DataManager.Core.SQL.Definition、DataManager.Runtime.SQL.Definition、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using System;
using System.Collections.Generic;

namespace DataManager.Runtime.SQL
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

        /// <summary>
        /// 初始化 SQLManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
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
 
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
        /// </summary>
        public override void Config(List<SQLParam> param)
        {
            foreach (var item in param)
                Create(item);
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// 正文中的关键协作调用包括 ThrowIfNull、Validate、Contains、Create、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// </summary>
        public override ISQL Create(SQLParam param)
        {
            throw new NotSupportedException("Please use Create<T> method instead.");
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetName。
        /// </summary>
        protected override string GetName(ISQL device)
        {
            return device.SQLParam.Name;
        }
    }
}
