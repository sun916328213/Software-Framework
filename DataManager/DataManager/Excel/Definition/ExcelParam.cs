using DataManager.Excel.Definition;

namespace DataManager.Excel.Definition
{
    public class ExcelParam
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = "Excel";

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } 
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 是否包含表头
        /// </summary>
        public bool HasHeader { get; set; } = true;

        /// <summary>
        /// 保存天数
        /// </summary>
        public int SaveDays { get; set; } = 180;

        /// <summary>
        /// Excel工具类型
        /// </summary>
        public ExcelType ToolType { get; set; } = ExcelType.MiniExcel;
 
    }
}