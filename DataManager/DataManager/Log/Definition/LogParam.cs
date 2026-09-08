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
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
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

        /// <summary>
        /// P2-3：参数校验，缺失必填项抛 ArgumentException。
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("LogParam.Name 不能为空。", nameof(Name));
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new ArgumentException("LogParam.FilePath 不能为空。", nameof(FilePath));
            if (string.IsNullOrWhiteSpace(Separator))
                throw new ArgumentException("LogParam.Separator 不能为空。", nameof(Separator));
        }
    }
}
