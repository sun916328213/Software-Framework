// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：MiniExcelHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Runtime、DataManager.Core、DataManager.Core.Excel、DataManager.Runtime.Excel、DataManager.Core.Excel.Definition、System。导入不代表所有类型都会在运行时加载。
using DataManager.Runtime;
using DataManager.Core;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Excel.Definition;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DataManager.Runtime.Excel.MiniExcel
{
    /// <summary>
    /// MiniExcelHelper 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：ExcelBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class MiniExcelHelper : ExcelBase
    {
        public override ExcelParam ExcelParam { get; set; } = new();

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 Combine、Exists、Empty、Query；具体条件和先后次序见方法体。
        /// </summary>
        public override IEnumerable<T> Query<T>()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            return MiniExcelLibs.MiniExcel.Query<T>(path);
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 Combine、Exists、Empty、Query、Where；具体条件和先后次序见方法体。
        /// </summary>
        public override IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            var data = MiniExcelLibs.MiniExcel.Query<T>(path);
            return data.Where(predicate);
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// 正文中的关键协作调用包括 Combine、Exists、Empty、QueryAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task<IEnumerable<T>> QueryAsync<T>()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            return await MiniExcelLibs.MiniExcel.QueryAsync<T>(path);
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// 正文中的关键协作调用包括 Combine、Exists、Empty、QueryAsync、Where；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            var data = await MiniExcelLibs.MiniExcel.QueryAsync<T>(path);
            return data.Where(predicate);
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 Update。
        /// </summary>
        public override void Update<T>(Func<T, bool> predicate, T updatedata)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        /// <summary>
        /// 更新当前实现管理的数据或状态；对应入口为 UpdateAsync。
        /// </summary>
        public override Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        /// <summary>
        /// 执行添加操作；对应入口为 AddRange。
        /// </summary>
        public override void AddRange<T>(IEnumerable<T> data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }
        /// <summary>
        /// 执行添加操作；对应入口为 AddRangeAsync。
        /// </summary>
        public override Task AddRangeAsync<T>(IEnumerable<T> data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }



        /// <summary>
        /// 执行写入操作；对应入口为 Write。
        /// 正文中的关键协作调用包括 Combine、EnsureDirectoryExists、SaveAs；具体条件和先后次序见方法体。
        /// </summary>
        public override void Write<T>(IEnumerable<T> datas)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);
            MiniExcelLibs.MiniExcel.SaveAs(path, datas);
        }

        /// <summary>
        /// 执行写入操作；对应入口为 WriteAsync。
        /// 正文中的关键协作调用包括 Combine、EnsureDirectoryExists、SaveAsAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task WriteAsync<T>(IEnumerable<T> datas)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);
            await MiniExcelLibs.MiniExcel.SaveAsAsync(path, datas);
        }

        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// </summary>
        public override void Add<T>(T data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        /// <summary>
        /// 执行添加操作；对应入口为 AddAsync。
        /// </summary>
        public override Task AddAsync<T>(T data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 EnsureDirectoryExists。
        /// 正文中的关键协作调用包括 GetDirectoryName、IsNullOrEmpty、Exists、CreateDirectory；具体条件和先后次序见方法体。
        /// </summary>
        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}