using DataManager.Log.Definition;
using Extension.Struct;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataManager.Log
{
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
