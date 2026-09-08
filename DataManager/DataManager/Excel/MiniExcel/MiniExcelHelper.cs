using DataManager.Excel.Definition;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DataManager.Excel.MiniExcel
{
    public class MiniExcelHelper : ExcelBase
    {
        public override ExcelParam ExcelParam { get; set; } = new();

        public override IEnumerable<T> Query<T>()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            return MiniExcelLibs.MiniExcel.Query<T>(path);
        }

        public override IEnumerable<T> Query<T>(Func<T, bool> predicate)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            var data = MiniExcelLibs.MiniExcel.Query<T>(path);
            return data.Where(predicate);
        }

        public override async Task<IEnumerable<T>> QueryAsync<T>()
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            return await MiniExcelLibs.MiniExcel.QueryAsync<T>(path);
        }

        public override async Task<IEnumerable<T>> QueryAsync<T>(Func<T, bool> predicate)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            if (!File.Exists(path))
                return Enumerable.Empty<T>();
            var data = await MiniExcelLibs.MiniExcel.QueryAsync<T>(path);
            return data.Where(predicate);
        }

        public override void Update<T>(Func<T, bool> predicate, T updatedata)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        public override Task UpdateAsync<T>(Func<T, bool> predicate, T updatedata)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        public override void AddRange<T>(IEnumerable<T> data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }
        public override Task AddRangeAsync<T>(IEnumerable<T> data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }



        public override void Write<T>(IEnumerable<T> datas)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);
            MiniExcelLibs.MiniExcel.SaveAs(path, datas);
        }

        public override async Task WriteAsync<T>(IEnumerable<T> datas)
        {
            string path = Path.Combine(ExcelParam.FilePath, ExcelParam.FileName);
            EnsureDirectoryExists(path);
            await MiniExcelLibs.MiniExcel.SaveAsAsync(path, datas);
        }

        public override void Add<T>(T data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        public override Task AddAsync<T>(T data)
        {
            throw new NotSupportedException("MiniExcel仅支持Write/Query，请用EPPlus做追加更新。");
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}