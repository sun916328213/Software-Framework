// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：IExcelTestEntity、ExcelPageModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、CommunityToolkit.Mvvm.ComponentModel、DataManager.Core.Excel.Definition、DataManager.Core.Log.Definition、System.Collections.ObjectModel。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using DataManager.Controls.Configuration;
using DataManager.Core.Excel.Definition;
using DataManager.Core.Log.Definition;
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
        /// <summary>生产默认落盘位置（<see cref="ConfigRoot.Default"/>，即 &lt;BaseDirectory&gt;\Config）。</summary>
        public ExcelPageModel() : this(ConfigRoot.Default)
        {
        }

        /// <summary>
        /// 初始化 ExcelPageModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 测试/工作台会话传入自己的 <see cref="ConfigRoot"/> 即可把配置目录隔离到临时位置。
        /// </summary>
        public ExcelPageModel(ConfigRoot configRoot)
        {
            ArgumentNullException.ThrowIfNull(configRoot);
            FilePath = configRoot.Directory;
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 SelectedProfile。
        /// 正文中的关键协作调用包括 FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        public ExcelParam SelectedProfile() =>
            SelectedExcelParam ?? DataCollection.FirstOrDefault() ?? throw new InvalidOperationException("请先添加文件配置档案");
    }
}
