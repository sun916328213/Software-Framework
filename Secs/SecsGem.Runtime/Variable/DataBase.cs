using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Secs.Variable
{
    public abstract class DataBase<T> : IData<T> where T : new()
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public virtual string FilePath { get; set; } = "Config//SecsJson//VID";

        public abstract string FileName { get; set; }

        public virtual ConcurrentList<T> Datas { get; set; } = new ConcurrentList<T>();



        public virtual void Load()
        {
            string path = Path.Combine(FilePath, FileName);
            if (!File.Exists(path)) return;

            var datas = CSVEx.ReadCsv<T>(path);

            Datas.Clear();
            foreach (var item in datas)
            {
                Datas.Add(item);
            }
        }



        /// <summary>
        /// 保存数据到文件（同步落盘）。
        /// 修复：原实现 fire-and-forget 调用异步导出，保存是否成功不可知、进程退出可能丢配置；
        /// 现改为先写临时文件、成功后原子替换，保证目标文件要么是完整旧版、要么是完整新版。
        /// </summary>
        public virtual void Save()
        {
            string path = Path.Combine(FilePath, FileName);

            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 先写临时文件（ExportToCsvAsync 内部已全部 ConfigureAwait(false)，不会捕获调用方上下文，可安全阻塞等待）
            string tmp = full + ".tmp";
            CSVEx.ExportToCsvAsync(Datas, tmp).GetAwaiter().GetResult();

            // 原子替换
            if (File.Exists(full))
            {
                File.Replace(tmp, full, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, full);
            }
        }


        public virtual List<T> GetDatas()
        {
            return new List<T>(Datas);
        }
    }
}
