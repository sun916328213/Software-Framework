using DataManager.Excel.Definition;
using DataManager.Excel.MiniExcel;
using DataManager.Excel.EPPlus;
using System;
using System.Collections.Generic;

namespace DataManager.Excel
{
    public class ExcelManager : ManagerBase<IExcel, ExcelParam>
    {
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
            switch (param.ToolType)
            {
                case ExcelType.MiniExcel:
                    var miniExcel = new MiniExcelHelper() { ExcelParam = param };
                    Add(miniExcel);
                    return miniExcel;
                case ExcelType.EPPlus:
                    var epPlus = new EPPlusHelper() { ExcelParam = param };
                    Add(epPlus);
                    return epPlus;
                default: throw new NotSupportedException($"{param.ToolType} not support.");
            }
        }

        protected override string GetName(IExcel device)
        {
            return device.ExcelParam.Name;
        }
    }
}