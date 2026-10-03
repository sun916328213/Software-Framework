// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：EPPlusHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Excel、DataManager.Runtime.Excel、DataManager.Core.Excel.Definition、OfficeOpenXml。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Excel.Definition;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DataManager.Runtime.Excel.EPPlus
{
    /// <summary>
    /// EPPlusHelper 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：ExcelBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class EPPlusHelper : ExcelBase
    {
       
        public override ExcelParam ExcelParam { get; set; } = new();

        // P1-5：PropertyInfo 缓存，避免每次反射
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propsCache = new();

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetProps。
        /// 正文中的关键协作调用包括 GetOrAdd、GetProperties；具体条件和先后次序见方法体。
        /// </summary>
        private static PropertyInfo[] GetProps<T>() =>
            _propsCache.GetOrAdd(typeof(T), t => t.GetProperties());

        /// <summary>
        /// 初始化 EPPlusHelper 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SetNonCommercialPersonal；具体条件和先后次序见方法体。
        /// </summary>
        public EPPlusHelper()
        {
            // 核心：声明非商业许可证（必须配置，否则运行时警告/限制）
            ExcelPackage.License.SetNonCommercialPersonal("SZST");
        }

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// 正文中的关键协作调用包括 AddRange；具体条件和先后次序见方法体。
        /// </summary>
        public override void Add<T>(T data)
        {
            AddRange(new[] { data });
        }

        /// <summary>
        /// 执行添加操作；对应入口为 AddAsync。
        /// 正文中的关键协作调用包括 AddRangeAsync；具体条件和先后次序见方法体。
        /// </summary>
        public override Task AddAsync<T>(T data)
        {
            return AddRangeAsync(new[] { data });
        }

        /// <summary>
        /// 执行添加操作；对应入口为 AddRange。
        /// 正文中的关键协作调用包括 Any、Combine、EnsureDirectoryExists、GetOrCreateWorksheet、GetProps；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行添加操作；对应入口为 AddRangeAsync。
        /// 正文中的关键协作调用包括 AddRange；具体条件和先后次序见方法体。
        /// </summary>
        public override Task AddRangeAsync<T>(IEnumerable<T> data)
        {
            AddRange(data);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 Combine、Exists、Empty、FirstOrDefault、WorksheetToEnumerable；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 Query、Where；具体条件和先后次序见方法体。
        /// </summary>
        public override IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            var data = Query<T>();
            return data.Where(predicate);
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// 正文中的关键协作调用包括 FromResult、Query；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<IEnumerable<T>> QueryAsync<T>()
        {
            return Task.FromResult(Query<T>());
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// 正文中的关键协作调用包括 Query、FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate)
        {
            var data = Query<T>(predicate);
            return Task.FromResult(data);
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// 正文中的关键协作调用包括 Combine、Exists、FirstOrDefault、GetProps、GetHeaders；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateAsync。
        /// 正文中的关键协作调用包括 Update；具体条件和先后次序见方法体。
        /// </summary>
        public override Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata)
        {
            Update(predicate, updatedata);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// 正文中的关键协作调用包括 Any、Combine、EnsureDirectoryExists、ClearWorkbook、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// 正文中的关键协作调用包括 Write；具体条件和先后次序见方法体。
        /// </summary>
        public override Task WriteAsync<T>(IEnumerable<T> datas)
        {
            Write(datas);
            return Task.CompletedTask;
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetOrCreateWorksheet。
        /// 正文中的关键协作调用包括 FirstOrDefault、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行写入操作；对应入口为 WriteHeaders。
        /// 正文中的关键协作调用包括 SetColor、BorderAround；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 执行写入操作；对应入口为 WriteData。
        /// 正文中的关键协作调用包括 GetValue、BorderAround；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 WorksheetToEnumerable。
        /// 正文中的关键协作调用包括 GetProps、Empty、GetHeaders、CreateItem、Add；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetHeaders。
        /// 正文中的关键协作调用包括 IsNullOrEmpty；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateItem。
        /// 正文中的关键协作调用包括 TryGetValue、SetValue、SafeConvert；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
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

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateTempItem。
        /// 正文中的关键协作调用包括 CreateItem；具体条件和先后次序见方法体。
        /// </summary>
        private T? CreateTempItem<T>(ExcelWorksheet worksheet, PropertyInfo[] properties, Dictionary<string, int> headers, int row) where T : new()
        {
            return CreateItem<T>(worksheet, properties, headers, row);
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateRow。
        /// 正文中的关键协作调用包括 TryGetValue、GetValue；具体条件和先后次序见方法体。
        /// </summary>
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

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ClearWorkbook。
        /// 正文中的关键协作调用包括 Delete；具体条件和先后次序见方法体。
        /// </summary>
        private void ClearWorkbook(ExcelPackage package)
        {
            // 移除所有工作表
            while (package.Workbook.Worksheets.Count > 0)
            {
                package.Workbook.Worksheets.Delete(0);
            }
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 AutoFitColumns。
        /// 正文中的关键协作调用包括 AutoFit、Column；具体条件和先后次序见方法体。
        /// </summary>
        private void AutoFitColumns(ExcelWorksheet worksheet, int columnCount)
        {
            for (int i = 1; i <= columnCount; i++)
            {
                worksheet.Column(i).AutoFit();
            }
        }
    }
}
