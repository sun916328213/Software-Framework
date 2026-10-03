// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ELogType。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log、System、System.Collections.Generic、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.Core.Log.Definition
{
    /// <summary>
    /// Log库类型
    /// </summary>
    public enum ELogType
    {
        SeriLog,
        NLog,
        Log4Net
    }
}
