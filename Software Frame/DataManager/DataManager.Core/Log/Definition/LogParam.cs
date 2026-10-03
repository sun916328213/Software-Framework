// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogParam。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log、CommunityToolkit.Mvvm.ComponentModel、System。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DataManager.Core.Log.Definition
{
    /// <summary>
    /// 日志参数（可观察模型，与 SQLParam 同款：工作台表格可直绑，JSON 字段名不变）。
    /// </summary>
    public partial class LogParam : ObservableObject
    {
        /// <summary>
        /// 名称
        /// </summary>
        [ObservableProperty]
        private string name = string.Empty;
        /// <summary>
        /// 文件路径
        /// </summary>
        [ObservableProperty]
        private string filePath = string.Empty;
        /// <summary>
        /// 间隔符号
        /// </summary>
        [ObservableProperty]
        private string separator = "||";
        /// <summary>
        /// 保存天数
        /// </summary>
        [ObservableProperty]
        private int saveDays = 180;
        /// <summary>
        /// Log类型
        /// </summary>
        [ObservableProperty]
        private ELogType logType = ELogType.SeriLog;

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
