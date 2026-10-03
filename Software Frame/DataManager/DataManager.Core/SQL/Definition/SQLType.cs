// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ESQLType、ESQLToolType。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.SQL、System、System.Collections.Generic、System.Linq、System.Text。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.Core.SQL.Definition
{
    /// <summary>
    /// ESQLType 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// </summary>
    public enum ESQLType
    {
        MySQL,
        PostgreSQL,
        SQLite,
        SQL_Server
    }

    /// <summary>
    /// ESQLToolType 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// </summary>
    public enum ESQLToolType
    {
        EFCore,
        SqlSugar,
        Dapper,
    }
}
