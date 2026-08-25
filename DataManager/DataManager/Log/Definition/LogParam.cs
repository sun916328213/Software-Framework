using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.Log.Definition
{
    public class LogParam
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } 
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// 间隔符号
        /// </summary>
        public string Separator { get; set; } = "||";
        /// <summary>
        /// 保存天数
        /// </summary>
        public int SaveDays { get; set; } = 180;
        /// <summary>
        /// Log类型
        /// </summary>
        public ELogType LogType { get; set; }= ELogType.SeriLog;
    }
}
