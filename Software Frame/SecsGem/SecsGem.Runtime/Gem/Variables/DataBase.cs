// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：DataBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.IO.Files、Extension.IO.Csv、Extension.IO.Retention、Extension.Core.Collections。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
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

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// DataBase 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：IData&lt;T&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class DataBase<T> : IData<T> where T : new()
    {
        /// <summary>
        /// 文件路径。每个表一个目录（SVID/DVID/ECID/CEID/ReportID/Alarm），由子类各自指定。
        /// </summary>
        public abstract string FilePath { get; set; }

        public abstract string FileName { get; set; }

        public virtual ConcurrentList<T> Datas { get; set; } = new ConcurrentList<T>();



        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 Combine、Exists、ReadCsv、Clear、Add；具体条件和先后次序见方法体。
        /// </summary>
        public virtual void Load()
        {
            string path = Path.Combine(FilePath, FileName);
            // CSV 不存在时用内置默认项兜底：SecsGem.Workbench 等宿主根本不带 Config 目录，
            // 原实现直接跳过，VID/Report/CEID 全空 → S6F11 组包失败、控制状态事件无处可报。
            if (!File.Exists(path)) { LoadDefaults(); return; }

            var datas = CSVEx.ReadCsv<T>(path);

            Datas.Clear();
            foreach (var item in datas)
            {
                Datas.Add(item);
            }

            // 文件存在也要补：常用项（控制状态的 VID/报表/事件）就算在界面上被删掉，
            // 下次加载也得补回来，否则 S6F11 组包缺变量、控制状态事件无处可报。
            // 只补缺的 ID，不清用户自己的行、不覆盖用户改过的名字。
            EnsureDefaults();
        }



        /// <summary>
        /// CSV 缺失时载入的内置默认项。基类为空，由需要打底的具体表覆写。
        /// </summary>
        protected virtual void LoadDefaults() { }

        /// <summary>
        /// CSV 存在时，把被删掉的受保护项（常用项）按 ID 补回来。
        /// 基类空实现；SVID/ECID/ReportID/CEID 各自覆写，用户自己的行不受影响。
        /// </summary>
        protected virtual void EnsureDefaults() { }

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


        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetDatas。
        /// </summary>
        public virtual List<T> GetDatas()
        {
            return new List<T>(Datas);
        }
    }
}
