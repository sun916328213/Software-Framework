using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Excel.Definition;
using DataManager.Log.Definition;
using System.Collections.ObjectModel;
using System.IO;
using WPFCustomControls.Controls;

namespace DataManager.Controls.Models
{
    /// <summary>
    /// Excel 演示行契约（宿主定义实体并实现它）。
    /// </summary>
    public interface IExcelTestEntity
    {
        string Name { get; set; }
        int Age { get; set; }
        DateTime CreateTime { get; set; }
    }

    /// <summary>
    /// Excel 测试页状态 Model：继承通用配置管理模型（档案存 DataCollection，
    /// 落盘位置走基类 FilePath / FileName）。行实体由宿主定义（实现 <see cref="IExcelTestEntity"/>）。
    /// </summary>
    public partial class ExcelPageModel<T> : ConfigManagerModel<ExcelParam> where T : IExcelTestEntity
    {
        public ExcelPageModel()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            FileName = "ExcelConfig.json";
        }

        [ObservableProperty]
        private ObservableCollection<T> results = new();

        [ObservableProperty]
        private T? selectedEntity;

        [ObservableProperty]
        private ExcelParam? selectedExcelParam;

        /// <summary>当前档案（选中行；空即报“请先添加/选择”）。兼容旧调用：无选中时回退首行。</summary>
        public ExcelParam CurrentProfile() => SelectedProfile();

        public ExcelParam SelectedProfile() =>
            SelectedExcelParam ?? DataCollection.FirstOrDefault() ?? throw new InvalidOperationException("请先添加文件配置档案");
    }
}
