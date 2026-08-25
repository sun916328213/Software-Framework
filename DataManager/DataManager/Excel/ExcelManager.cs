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
            switch (param.ToolType)
            {
                case ExcelType.MiniExcel:
                    var miniExcel = new MiniExcelHelper() { ExcelParam = param };
                    DataManagers.Add(miniExcel);
                    return miniExcel;
                case ExcelType.EPPlus:
                    var epPlus = new EPPlusHelper() { ExcelParam = param };
                    DataManagers.Add(epPlus);
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