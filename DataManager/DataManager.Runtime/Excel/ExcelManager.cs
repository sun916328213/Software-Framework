using DataManager.Excel.Definition;
using System;
using System.Collections.Generic;

namespace DataManager.Excel
{
    /// <summary>
    /// P12：实例创建经 <see cref="IExcelProviderFactory"/> 可替换接缝；无参构造默认使用 Runtime 内建工厂，
    /// 旧调用 <c>new ExcelManager()</c> 与 <c>Create(ExcelParam)</c> 签名行为保持。
    /// </summary>
    public class ExcelManager : ManagerBase<IExcel, ExcelParam>, IExcelManager
    {
        private readonly IExcelProviderFactory _factory;

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

        public override void Config(List<ExcelParam> param)
        {
            foreach (var item in param)
            {
                Create(item);
            }
        }

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

        protected override string GetName(IExcel device)
        {
            return device.ExcelParam.Name;
        }
    }
}
