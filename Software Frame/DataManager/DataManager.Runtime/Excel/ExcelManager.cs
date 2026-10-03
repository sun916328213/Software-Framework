// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ExcelManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Excel、DataManager.Core.Excel.Definition、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Core.Excel.Definition;
using System;
using System.Collections.Generic;

namespace DataManager.Runtime.Excel
{
    /// <summary>
    /// P12：实例创建经 <see cref="IExcelProviderFactory"/> 可替换接缝；无参构造默认使用 Runtime 内建工厂，
    /// 旧调用 <c>new ExcelManager()</c> 与 <c>Create(ExcelParam)</c> 签名行为保持。
    /// </summary>
    public class ExcelManager : ManagerBase<IExcel, ExcelParam>, IExcelManager
    {
        private readonly IExcelProviderFactory _factory;

        /// <summary>
        /// 初始化 ExcelManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ExcelManager()
            : this(new ExcelProviderFactory())
        {
        }

        /// <summary>
        /// 使用可注入的 Provider 工厂创建管理器；测试可传入 Fake/Stub，无需真实实现。
        /// </summary>
        public ExcelManager(IExcelProviderFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Config。
        /// 正文中的关键协作调用包括 Create；具体条件和先后次序见方法体。
        /// </summary>
        public override void Config(List<ExcelParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 Create。
        /// 正文中的关键协作调用包括 ThrowIfNull、Validate、Contains、Create、Add；具体条件和先后次序见方法体。
        /// </summary>
        public override IExcel Create(ExcelParam param)
        {
            ArgumentNullException.ThrowIfNull(param);
            param.Validate();
            if (Contains(param.Name))
                throw new InvalidOperationException($"已存在同名Excel实例: {param.Name}");
            var excel = _factory.Create(param);
            Add(excel);
            return excel;
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetName。
        /// </summary>
        protected override string GetName(IExcel device)
        {
            return device.ExcelParam.Name;
        }
    }
}
