using DataManager.Excel.Definition;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DataManager.Excel.EPPlus
{
    public class EPPlusHelper : ExcelBase
    {
       
        public override ExcelParam ExcelParam { get; set; } = new();

        // P1-5：PropertyInfo 缓存，避免每次反射
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();

        private static PropertyInfo[] GetProps<T>() =>
            _propsCache.GetOrAdd(typeof(T), t => t.GetProperties());

        // P1-5：有状态常驻包已废弃，主流程均为无状态 using；保留仅为兼容旧调用
        [Obsolete("有状态API已废弃，主流程为无状态，请使用Write/Query/Add/Update。")]
        public ExcelPackage? ExcelPackage { get; private set; }
        [Obsolete("有状态API已废弃，主流程为无状态，请使用Write/Query/Add/Update。")]
        public ExcelWorksheet? CurrentWorksheet { get; private set; }

        public EPPlusHelper()
        {
            // 核心：声明非商业许可证（必须配置，否则运行时警告/限制）
            ExcelPackage.License.SetNonCommercialPersonal("SZST");
        }

        public override void Add<T>(T data)
        {
            AddRange(new[] { data });
        }

        public override Task AddAsync<T>(T data)
        {
            return AddRangeAsync(new[] { data });
        }

        public override void AddRange<T>(IEnumerable<T> data)
        {
            if (data == null || !data.Any())
                return;

            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);

            using var package = new ExcelPackage(new FileInfo(path));
            var worksheet = GetOrCreateWorksheet(package);

            var properties = GetProps<T>();
            var headerRow = 1;
            int startRow;

            // 检查是否有表头
            if (worksheet.Dimension == null || worksheet.Dimension.Rows == 0)
            {
                // 写入表头并设置样式
                WriteHeaders(worksheet, properties, headerRow);
                startRow = headerRow + 1;
            }
            else
            {
                // P1-5：用实际有值最后一行+1，避免样式幽灵行/覆盖最后一行
                startRow = GetLastUsedRow(worksheet) + 1;
            }

            // 写入数据
            WriteData(worksheet, data, properties, startRow);
            package.Save();
        }

        public override Task AddRangeAsync<T>(IEnumerable<T> data)
        {
            AddRange(data);
            return Task.CompletedTask;
        }

        public override IEnumerable<T> Query<T>()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();

            using var package = new ExcelPackage(new FileInfo(path));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return Enumerable.Empty<T>();

            return WorksheetToEnumerable<T>(worksheet);
        }

        public override IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            var data = Query<T>();
            return data.Where(predicate);
        }

        public override Task<IEnumerable<T>> QueryAsync<T>()
        {
            return Task.FromResult(Query<T>());
        }

        public override Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate)
        {
            var data = Query<T>(predicate);
            return Task.FromResult(data);
        }

        public override void Update<T>(Func<T, bool> predicate, T updatedata)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return;

            using var package = new ExcelPackage(new FileInfo(path));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null || worksheet.Dimension == null)
                return;

            var properties = GetProps<T>();
            var headerRow = 1;

            var headers = GetHeaders(worksheet, headerRow);
            if (headers.Count == 0)
                return;

            // 遍历数据行，更新符合条件的数据
            for (int row = headerRow + 1; row <= worksheet.Dimension.Rows; row++)
            {
                // 创建临时对象用于检查条件
                var tempItem = CreateTempItem<T>(worksheet, properties, headers, row);
                if (tempItem == null)
                    continue;

                // 检查是否符合条件
                if (predicate(tempItem))
                {
                    // 更新数据
                    UpdateRow(worksheet, updatedata, properties, headers, row);
                }
            }

            package.Save();
        }

        public override Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata)
        {
            Update(predicate, updatedata);
            return Task.CompletedTask;
        }

        public override void Write<T>(IEnumerable<T> datas)
        {
            if (datas == null || !datas.Any())
                return;

            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);

            using var package = new ExcelPackage(new FileInfo(path));
            // 清空工作簿，只保留一个工作表
            ClearWorkbook(package);
            var worksheet = package.Workbook.Worksheets.Add("Sheet1");
            
            var properties = GetProps<T>();
            var headerRow = 1;

            // 写入表头并设置样式
            WriteHeaders(worksheet, properties, headerRow);

            // 写入数据
            WriteData(worksheet, datas, properties, headerRow + 1);

            // 自动调整列宽
            AutoFitColumns(worksheet, properties.Length);

            package.Save();
        }

        public override Task WriteAsync<T>(IEnumerable<T> datas)
        {
            Write(datas);
            return Task.CompletedTask;
        }

        // EPPlus特有功能：打开Excel文件
        [Obsolete("有状态API已废弃，主流程为无状态。")]
        public void OpenExcel()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (File.Exists(path))
            {
                ExcelPackage = new ExcelPackage(new FileInfo(path));
                CurrentWorksheet = ExcelPackage.Workbook.Worksheets.FirstOrDefault() ?? ExcelPackage.Workbook.Worksheets.Add("Sheet1");
            }
        }

        // EPPlus特有功能：保存Excel文件
        [Obsolete("有状态API已废弃，主流程为无状态。")]
        public void SaveExcel()
        {
            if (ExcelPackage != null)
            {
                ExcelPackage.Save();
            }
        }

        // EPPlus特有功能：关闭Excel文件
        [Obsolete("有状态API已废弃，主流程为无状态。")]
        public void CloseExcel()
        {
            if (ExcelPackage != null)
            {
                ExcelPackage.Dispose();
                ExcelPackage = null;
                CurrentWorksheet = null;
            }
        }

        // EPPlus特有功能：设置单元格样式
        [Obsolete("有状态API已废弃，主流程为无状态。")]
        public void SetCellStyle(int row, int col, Action<ExcelStyle> styleAction)
        {
            if (CurrentWorksheet != null)
            {
                var cell = CurrentWorksheet.Cells[row, col];
                styleAction(cell.Style);
            }
        }

        // EPPlus特有功能：添加图表
        [Obsolete("有状态API已废弃，主流程为无状态。")]
        public void AddChart(string chartTitle, int startRow, int startCol, int endRow, int endCol)
        {
            if (CurrentWorksheet != null)
            {
                var chart = CurrentWorksheet.Drawings.AddChart(chartTitle, OfficeOpenXml.Drawing.Chart.eChartType.ColumnClustered);
                chart.SetPosition(startRow - 1, 0, startCol - 1, 0);
                chart.SetSize(600, 400);
                chart.Title.Text = chartTitle;
                
                var series = chart.Series.Add(CurrentWorksheet.Cells[2, endCol, endRow, endCol], CurrentWorksheet.Cells[2, startCol, endRow, startCol]);
                series.Header = CurrentWorksheet.Cells[1, endCol].Value?.ToString();
            }
        }

        // 辅助方法
        private void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private ExcelWorksheet GetOrCreateWorksheet(ExcelPackage package)
        {
            return package.Workbook.Worksheets.FirstOrDefault() ?? package.Workbook.Worksheets.Add("Sheet1");
        }

        /// <summary>
        /// 获取工作表中实际有值的最后一行（忽略仅有样式的幽灵单元格）
        /// </summary>
        private int GetLastUsedRow(ExcelWorksheet worksheet)
        {
            var dim = worksheet.Dimension;
            if (dim == null) return 1;

            for (int row = dim.End.Row; row >= 1; row--)
            {
                for (int col = 1; col <= dim.End.Column; col++)
                {
                    if (worksheet.Cells[row, col].Value != null)
                        return row;
                }
            }
            return 1;
        }

        private void WriteHeaders(ExcelWorksheet worksheet, PropertyInfo[] properties, int headerRow)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                var cell = worksheet.Cells[headerRow, i + 1];
                cell.Value = properties[i].Name;
                // 设置表头样式
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        private void WriteData<T>(ExcelWorksheet worksheet, IEnumerable<T> data, PropertyInfo[] properties, int startRow)
        {
            int row = startRow;
            foreach (var item in data)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    var value = properties[i].GetValue(item);
                    worksheet.Cells[row, i + 1].Value = value;
                    // 设置单元格边框
                    worksheet.Cells[row, i + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }
                row++;
            }
        }

        private IEnumerable<T> WorksheetToEnumerable<T>(ExcelWorksheet worksheet) where T : new()
        {
            var properties = GetProps<T>();
            var headerRow = 1;
            var dataRows = worksheet.Dimension?.Rows - headerRow ?? 0;

            if (dataRows <= 0)
                return Enumerable.Empty<T>();

            var headers = GetHeaders(worksheet, headerRow);
            if (headers.Count == 0)
                return Enumerable.Empty<T>();

            var result = new List<T>();
            var dim = worksheet.Dimension;
            if (dim == null)
                return Enumerable.Empty<T>();
            for (int row = headerRow + 1; row <= dim.Rows; row++)
            {
                var item = CreateItem<T>(worksheet, properties, headers, row);
                if (item != null)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        private Dictionary<string, int> GetHeaders(ExcelWorksheet worksheet, int headerRow)
        {
            var headers = new Dictionary<string, int>();
            var dim = worksheet.Dimension;
            if (dim == null)
                return headers;
            for (int col = 1; col <= dim.Columns; col++)
            {
                var header = worksheet.Cells[headerRow, col].Value?.ToString();
                if (!string.IsNullOrEmpty(header))
                {
                    headers[header] = col;
                }
            }
            return headers;
        }

        private T? CreateItem<T>(ExcelWorksheet worksheet, PropertyInfo[] properties, Dictionary<string, int> headers, int row) where T : new()
        {
            var item = new T();
            var hasValue = false;
            foreach (var prop in properties)
            {
                if (headers.TryGetValue(prop.Name, out int col))
                {
                    var cellValue = worksheet.Cells[row, col].Value;
                    if (cellValue != null)
                    {
                        hasValue = true;
                        try
                        {
                            prop.SetValue(item, SafeConvert(cellValue, prop.PropertyType));
                        }
                        catch { }
                    }
                }
            }
            // 该行所有单元格都无值（样式导致的幽灵行）时返回 null，避免查询多出空实体
            return hasValue ? item : default;
        }

        /// <summary>
        /// P1-5：支持 Nullable/Enum/DateTime/Guid/bool 数值互转的类型转换。
        /// </summary>
        private static object? SafeConvert(object value, Type targetType)
        {
            if (value == null) return null;
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlying.IsInstanceOfType(value)) return value;
            if (underlying.IsEnum)
            {
                if (value is string s) return Enum.Parse(underlying, s, ignoreCase: true);
                return Enum.ToObject(underlying, value);
            }
            if (underlying == typeof(DateTime))
            {
                if (value is DateTime dt) return dt;
                if (value is double oa) return DateTime.FromOADate(oa);
                return Convert.ChangeType(value, underlying);
            }
            if (underlying == typeof(Guid))
            {
                if (value is string gs) return Guid.Parse(gs);
                return new Guid(value.ToString()!);
            }
            if (underlying == typeof(bool))
            {
                if (value is double d) return d != 0;
                if (value is string bs && double.TryParse(bs, out var bd)) return bd != 0;
            }
            return Convert.ChangeType(value, underlying);
        }

        private T? CreateTempItem<T>(ExcelWorksheet worksheet, PropertyInfo[] properties, Dictionary<string, int> headers, int row) where T : new()
        {
            return CreateItem<T>(worksheet, properties, headers, row);
        }

        private void UpdateRow<T>(ExcelWorksheet worksheet, T updatedata, PropertyInfo[] properties, Dictionary<string, int> headers, int row)
        {
            foreach (var prop in properties)
            {
                if (headers.TryGetValue(prop.Name, out int col))
                {
                    var value = prop.GetValue(updatedata);
                    worksheet.Cells[row, col].Value = value;
                }
            }
        }

        private void ClearWorkbook(ExcelPackage package)
        {
            // 移除所有工作表
            while (package.Workbook.Worksheets.Count > 0)
            {
                package.Workbook.Worksheets.Delete(0);
            }
        }

        private void AutoFitColumns(ExcelWorksheet worksheet, int columnCount)
        {
            for (int i = 1; i <= columnCount; i++)
            {
                worksheet.Column(i).AutoFit();
            }
        }
    }
}
