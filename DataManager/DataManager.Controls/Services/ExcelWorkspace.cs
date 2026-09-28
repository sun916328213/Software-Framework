using DataManager.Excel;
using Extension.UI.Controls;
using Extension.Core.Conversion;
using DataManager.Excel.Definition;
using System.Collections.ObjectModel;
using System.IO;

namespace DataManager.Controls.Services
{
    /// <summary>
    /// Excel 实际流程（对标 WPFControl 的 DataQueryService：持有 <see cref="Models.ExcelPageModel{T}"/> 并读写它）。
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
        public Models.ExcelPageModel<T> Model { get; } = new();

        public ExcelWorkspace(IExcelManager excelManager)
        {
            _excelManager = excelManager;
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

        public void ApplyProfile()
        {
            var param = Model.CurrentProfile();
            ResetExcel(param);
        }

        public void OverwriteWithSample(int count)
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.Write(Sample(count));
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        public void AppendSample()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.Add(Sample(1)[0]);
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        public void AppendSampleBatch(int count)
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            excel.AddRange(Sample(count));
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(excel.Query<T>().ToList()));
        }

        public void QueryAll()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            var data = excel.Query<T>().ToList();
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(data));
        }

        public void QueryAdults()
        {
            var param = Model.CurrentProfile();
            var (excel, _) = EnsureExcel(param);
            var data = excel.Query<T>(x => x.Age > 25).ToList();
            DispatcherEx.UIInvokeSafe(() => Model.Results = new ObservableCollection<T>(data));
        }

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
