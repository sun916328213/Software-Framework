using DataManager.Excel;
using DataManager.Excel.Definition;
using DataManager.Workbench.Models;
using System.IO;

namespace DataManager.Workbench.Services
{
    /// <summary>
    /// Excel 实际流程（ViewModel 只剩绑定与命令装配）。
    /// 职责：Excel 实例生命周期（指纹重建）、测试数据生成、读写删改查；
    /// 不碰 UI：计时/日志输出/结果集全部由 ViewModel 包装完成。
    /// </summary>
    public sealed class ExcelWorkspace
    {
        private readonly ExcelManager _excelManager;
        private IExcel? _excel;
        private string? _currentKey;

        public ExcelWorkspace(ExcelManager excelManager)
        {
            _excelManager = excelManager;
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

        public IReadOnlyList<ExcelPerson> OverwriteWithSample(ExcelParam param, int count)
        {
            var (excel, _) = EnsureExcel(param);
            excel.Write(Sample(count));
            return excel.Query<ExcelPerson>().ToList();
        }

        public IReadOnlyList<ExcelPerson> AppendSample(ExcelParam param)
        {
            var (excel, _) = EnsureExcel(param);
            excel.Add(Sample(1)[0]);
            return excel.Query<ExcelPerson>().ToList();
        }

        public IReadOnlyList<ExcelPerson> AppendSampleBatch(ExcelParam param, int count)
        {
            var (excel, _) = EnsureExcel(param);
            excel.AddRange(Sample(count));
            return excel.Query<ExcelPerson>().ToList();
        }

        public IReadOnlyList<ExcelPerson> QueryAll(ExcelParam param)
        {
            var (excel, _) = EnsureExcel(param);
            return excel.Query<ExcelPerson>().ToList();
        }

        public IReadOnlyList<ExcelPerson> QueryAdults(ExcelParam param)
        {
            var (excel, _) = EnsureExcel(param);
            return excel.Query<ExcelPerson>(x => x.Age > 25).ToList();
        }

        public IReadOnlyList<ExcelPerson> UpdateSelected(ExcelParam param, ExcelPerson selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            var (excel, _) = EnsureExcel(param);
            excel.Update<ExcelPerson>(x => x.Name == selected.Name, new ExcelPerson
            {
                Name = selected.Name,
                Age = selected.Age + 1,
                CreateTime = DateTime.Now,
            });
            return excel.Query<ExcelPerson>().ToList();
        }

        private static List<ExcelPerson> Sample(int count) => Enumerable.Range(1, count)
            .Select(i => new ExcelPerson
            {
                Name = $"人员{i}",
                Age = 20 + (i * 3) % 30,
                CreateTime = DateTime.Today.AddDays(-i),
            })
            .ToList();
    }
}
