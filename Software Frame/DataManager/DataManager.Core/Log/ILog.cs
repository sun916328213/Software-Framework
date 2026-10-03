// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：ILog。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、DataManager.Core.Log.Definition、System、System.Collections.Generic、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using DataManager.Core.Log.Definition;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Core.Log
{
    /// <summary>
    /// ILog 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// </summary>
    public interface ILog
    {
        /// <summary>
        /// Log参数
        /// </summary>
        LogParam Param { get; set; }
        /// <summary>
        /// 添加日志信息
        /// </summary>
        /// <param name="info">日志信息</param>
        /// <param name="logLevel">日志信息等级</param>
        void Add(string info, ELogLevel logLevel = ELogLevel.Information);
        /// <summary>
        /// 添加日志信息 异步
        /// </summary>
        /// <param name="info">日志信息</param>
        /// <param name="logLevel">日志信息等级</param>
        Task AddAsync(string info, ELogLevel logLevel = ELogLevel.Information);
        /// <summary>
        /// 查询日志信息
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <returns>查询数据集合</returns>
        List<LogInfo> Query(string fileName);
        /// <summary>
        /// 查询日志信息 异步
        /// </summary>
        /// <param name="fileName">文件名称</param>
        /// <returns>查询数据集合</returns>
        Task<List<LogInfo>> QueryAsync(string fileName);
        /// <summary>
        /// 查询日志信息
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns></returns>
        List<LogInfo> Query(DateTime startTime, DateTime endTime);
        /// <summary>
        /// 查询日志信息 异步
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns></returns>
        Task<List<LogInfo>> QueryAsync(DateTime startTime, DateTime endTime);
        /// <summary>
        /// 查询日志信息 多行读取
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        List<string> Query_Multi(string filePath);
        /// <summary>
        /// 查询日志信息 多行读取
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        Task<List<string>> Query_MultiAsync(string filePath);
        /// <summary>
        /// 查询日志信息 多行读取 按照开始时间和结束时间
        /// </summary>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <returns></returns>
        List<string> Query_Multi(DateTime startTime, DateTime endTime);
        /// <summary>
        /// 查询日志信息 多行读取 按照开始时间和结束时间 异步
        /// </summary>
        /// <param name="start">开始时间</param>
        /// <param name="end">结束时间</param>
        /// <returns></returns>
        Task<List<string>> Query_MultiAsync(DateTime startTime, DateTime endTime);
    }
}
