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
        public string FilePath { get; set; } = string.Empty;
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

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

        /// <summary>
        /// P2-3：参数校验，缺失必填项抛 ArgumentException（非破坏性，替代 required 关键字）。
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("ExcelParam.Name 不能为空。", nameof(Name));
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new ArgumentException("ExcelParam.FilePath 不能为空。", nameof(FilePath));
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("ExcelParam.FileName 不能为空。", nameof(FileName));
        }
    }
}