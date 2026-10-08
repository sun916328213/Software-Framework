// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ExcelWorkspace。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Controls、DataManager.Core、DataManager.Core.Excel、DataManager.Runtime.Excel、Extension.UI.Controls、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using DataManager.Controls;
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using DataManager.Controls.Configuration;
using DataManager.Core.Excel.Definition;
using System.Collections.ObjectModel;
using System.IO;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// Excel 实际流程（对标 WPFControls 的 DataQueryService：持有 <see cref="Models.ExcelPageModel{T}"/> 并读写它）。
    /// 职责：Model 初始化、Excel 实例生命周期（指纹重建）、测试数据生成、读写删改查（含结果集回填）。
    /// 行实体由宿主定义（实现 <see cref="Models.IExcelTestEntity"/>）。
    /// </summary>
    public sealed class ExcelWorkspace<T>
        where T : class, Models.IExcelTestEntity, new()
    {
        private readonly IExcelManager _excelManager;
        private IExcel? _excel;
        private string? _currentKey;

        /// <summary>页状态（单例 Workspace 持有，VM 直接暴露）。</summary>
        public Models.ExcelPageModel<T> Model { get; }

        /// <summary>生产默认：配置落在 <see cref="ConfigRoot.Default"/>（即 &lt;BaseDirectory&gt;\Config）。</summary>
        public ExcelWorkspace(IExcelManager excelManager) : this(excelManager, ConfigRoot.Default)
        {
        }

        /// <summary>
        /// 初始化 ExcelWorkspace 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 LoadProfiles；具体条件和先后次序见方法体。
        /// 传入自己的 <see cref="ConfigRoot"/> 可把本工作区读写配置的目录隔离到临时位置。
        /// </summary>
        public ExcelWorkspace(IExcelManager excelManager, ConfigRoot configRoot)
        {
            ArgumentNullException.ThrowIfNull(configRoot);
            _excelManager = excelManager;
            Model = new Models.ExcelPageModel<T>(configRoot);
            LoadProfiles();
        }

        /// <summary>实例指纹：全部配置要素参与，任一改动都重建实例。</summary>
        public static string BuildExcelKey(ExcelParam param) =>
            $"{param.ToolType}:{param.Name}:{param.FilePath}:{param.FileName}:{param.HasHeader}";

        /// <summary>强制重建实例（应用配置命令用，顺带建目录）。</summary>
        public IExcel ResetExcel(ExcelParam param)
        {
            if (_excel != null)
            {
                _excelManager.Remove(_excel);
                _excel = null;
            }
            Directory.CreateDirectory(param.FilePath);
            _excel = _excelManager.Create(param);
            _currentKey = BuildExcelKey(param);
            return _excel;
        }

        /// <summary>按指纹确保实例（命中复用，否则重建；返回是否发生重建）。</summary>
        public (IExcel Excel, bool Rebuilt) EnsureExcel(ExcelParam param)
        {
            var key = BuildExcelKey(param);
            if (_excel != null && _currentKey == key)
                return (_excel, false);
            var changed = _excel != null;
            var excel = ResetExcel(param);
            return (excel, changed);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ApplyProfile。
        /// 正文中的关键协作调用包括 CurrentProfile、ResetExcel；具体条件和先后次序见方法体。
        /// </summary>
        public void ApplyProfile()
        {
            var param = Model.CurrentProfile();
            ResetExcel(param);
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 OverwriteWithSample。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureExcel、Write、Sample、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void OverwriteWithSample(int count)
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.Write(Sample(count));
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendSample。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureExcel、Add、Sample、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void AppendSample()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.Add(Sample(1)[0]);
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AppendSampleBatch。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureExcel、AddRange、Sample、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void AppendSampleBatch(int count)
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.AddRange(Sample(count));
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAll。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureExcel、ToList、Query、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void QueryAll()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            var data = excel.Query<T>().ToList();
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(data));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAdults。
        /// 正文中的关键协作调用包括 CurrentProfile、EnsureExcel、ToList、Query、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void QueryAdults()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            var data = excel.Query<T>(x => x.Age > 25).ToList();
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(data));
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateSelected。
        /// 正文中的关键协作调用包括 ThrowIfNull、CurrentProfile、EnsureExcel、Update、UIInvokeSafe；具体条件和先后次序见方法体。
        /// </summary>
        public void UpdateSelected(T selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.Update<T>(x => x.Name == selected.Name, new T
            {
                Name = selected.Name,
                Age = selected.Age + 1,
                CreateTime = DateTime.Now,
            });
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Sample。
        /// 正文中的关键协作调用包括 ToList、Select、Range、AddDays；具体条件和先后次序见方法体。
        /// </summary>
        private static List<T> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new T
            {
                Name = $"人员{i}",
                Age = 20 + (i * 3) % 30,
                CreateTime = DateTime.Today.AddDays(-i),
            })
            .ToList();

        // ---------- Model 初始化（读 Config 文件 → 默认行） ----------
        private void LoadProfiles()
        {
            var profiles = JsonEx.TryRead<List<ExcelParam>>(Model.FullPath!);
            if (profiles is { Count: > 0 })
                ReplaceProfiles(profiles.Where(p => !string.IsNullOrWhiteSpace(p.Name)));
            if (Model.DataCollection.Count == 0)
            {
                var fallback = new ExcelParam
                {
                    Name = "workbench",
                    FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "excel"),
                    FileName = "workbench.xlsx",
                };
                Model.DataCollection.Add(fallback);
                Model.SelectedExcelParam = fallback;
            }
            else
            {
                Model.SelectedExcelParam ??= Model.DataCollection.FirstOrDefault();
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReplaceProfiles。
        /// 正文中的关键协作调用包括 Clear、IsNullOrWhiteSpace、Add、FirstOrDefault；具体条件和先后次序见方法体。
        /// </summary>
        private void ReplaceProfiles(IEnumerable<ExcelParam> profiles)
        {
            Model.DataCollection.Clear();
            foreach (var p in profiles)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                    p.Name = $"文件{Model.DataCollection.Count + 1}";
                Model.DataCollection.Add(p);
            }
            Model.SelectedExcelParam = Model.DataCollection.FirstOrDefault();
        }
    }
}
