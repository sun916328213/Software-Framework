using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Excel.Definition;

namespace DataManager.Excel.Definition
{
    /// <summary>
    /// Excel 参数（可观察模型，与 SQLParam 同款：工作台表格可直绑，JSON 字段名不变）。
    /// </summary>
    public partial class ExcelParam : ObservableObject
    {
        /// <summary>
        /// 名称
        /// </summary>
        [ObservableProperty]
        private string name = "Excel";

        /// <summary>
        /// 文件路径
        /// </summary>
        [ObservableProperty]
        private string filePath = string.Empty;
        /// <summary>
        /// 文件名
        /// </summary>
        [ObservableProperty]
        private string fileName = string.Empty;

        /// <summary>
        /// 是否包含表头
        /// </summary>
        [ObservableProperty]
        private bool hasHeader = true;

        /// <summary>
        /// 保存天数
        /// </summary>
        [ObservableProperty]
        private int saveDays = 180;

        /// <summary>
        /// Excel工具类型
        /// </summary>
        [ObservableProperty]
        private ExcelType toolType = ExcelType.MiniExcel;

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