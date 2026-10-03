// 【中文阅读说明】
// DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
// 本文件定义：LogBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：DataManager.Core、System、System.Collections.Generic、System.IO、System.Linq、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using DataManager.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extension.Core.Conversion;
using DataManager.Core.Log.Definition;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;

namespace DataManager.Core.Log
{
    /// <summary>
    /// LogBase 的类型定义。DataManager 模块：数据访问契约、SQL/日志/Excel 运行能力及配置界面；具体数据库配置和实体由使用方决定。
    /// 继承或实现的契约：ILog。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class LogBase : ILog
    {
        public abstract LogParam  Param { get; set; }
 
        /// <summary>
        /// 执行添加操作；对应入口为 Add。
        /// </summary>
        public abstract void Add(string info, ELogLevel logLevel = ELogLevel.Information);

        /// <summary>
        /// P5-1：virtual 留缝。三家 sink（Serilog/NLog/log4net）均为同步 API，
        /// 基类只能线程池转交以避免阻塞调用方；未来若有真异步 sink 可 override。
        /// </summary>
        public virtual async Task AddAsync(string info, ELogLevel logLevel = ELogLevel.Information)
        {
             await Task.Run(() => Add(info, logLevel));
        }

        /// <summary>
        /// P5-1：纯解析抽取（同步/异步共用，行为单点）。
        /// </summary>
        protected static List<LogInfo> ParseLogInfos(string datas, string separator)
        {
            List<LogInfo> logInfos = new List<LogInfo>();
            var seps = separator.ToCharArray();
            var dataselect = datas.SplitByDateTime().Select(s => s.Split(seps).Where(x => x != string.Empty));
            foreach (var info in dataselect)
            {
                if (info.Count() >= 3)
                    logInfos.Add(new LogInfo(info.ElementAt(0), info.ElementAt(2).Trim(), (ELogLevel)Enum.Parse(typeof(ELogLevel), info.ElementAt(1))));
                else
                    logInfos.Add(new LogInfo() { Info = info.ElementAt(0) });
            }
            return logInfos;
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 Combine、ReadAllTextShared、ParseLogInfos；具体条件和先后次序见方法体。
        /// </summary>
        public virtual List<LogInfo> Query(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var datas = ReadAllTextShared(path);
            return ParseLogInfos(datas, Param.Separator);
        }

        /// <summary>
        /// P5-1：真异步读（FileStream 异步 IO + 共享读），解析为同步 CPU，不占线程池。
        /// </summary>
        public async Task<List<LogInfo>> QueryAsync(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var datas = await ReadAllTextSharedAsync(path);
            return ParseLogInfos(datas, Param.Separator);
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryFunc。
        /// 正文中的关键协作调用包括 JudgeDatetime、GetFiles、ToDateNOSapce、AddDays、func；具体条件和先后次序见方法体。
        /// </summary>
        private List<T> QueryFunc<T>(DateTime startTime, DateTime endTime,Func<string ,List<T>> func)
        {
            List<T> logInfos = new List<T>();
            if (endTime < startTime) throw new Exception("The start time is greater than the end time!!");

            TimeSpan timeSpan = JudgeDatetime(startTime, endTime);
            for (int index = 0; index < timeSpan.Days; index++)
            {
                var selectfile = Param.FilePath.GetFiles(startTime.AddDays(index).ToDateNOSapce());
                foreach (var item in selectfile)
                {
                    var data = func(item.FullName);
                    if (data != null)
                        logInfos.AddRange(data);
                }
            }
            return logInfos;
        }


        /// <summary>
        /// 执行查询操作；对应入口为 Query。
        /// 正文中的关键协作调用包括 QueryFunc、Query；具体条件和先后次序见方法体。
        /// </summary>
        public virtual List<LogInfo> Query(DateTime startTime, DateTime endTime)
        {
            return QueryFunc(startTime, endTime, x => Query(x));
        }




        /// <summary>
        /// 执行查询操作；对应入口为 QueryAsync。
        /// 正文中的关键协作调用包括 QueryFuncAsync、QueryAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<List<LogInfo>> QueryAsync(DateTime startTime, DateTime endTime)
        {
            return await QueryFuncAsync(startTime, endTime, x => QueryAsync(x));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 QueryFuncAsync。
        /// 正文中的关键协作调用包括 JudgeDatetime、GetFiles、ToDateNOSapce、AddDays、Add；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        private async Task<List<T>> QueryFuncAsync<T>(DateTime startTime, DateTime endTime, Func<string, Task<List<T>>> func)
        {
            if (endTime < startTime) throw new Exception("The start time is greater than the end time!!");

            TimeSpan timeSpan = JudgeDatetime(startTime, endTime);
            var tasks = new List<Task<List<T>>>();
            for (int index = 0; index < timeSpan.Days; index++)
            {
                var selectfile = Param.FilePath.GetFiles(startTime.AddDays(index).ToDateNOSapce());
                foreach (var item in selectfile)
                    tasks.Add(func(item.FullName));
            }
            var results = await Task.WhenAll(tasks);
            return results.Where(data => data != null).SelectMany(data => data!).ToList();
        }


        /// <summary>
        /// 执行查询操作；对应入口为 Query_Multi。
        /// 正文中的关键协作调用包括 Combine、ReadAllTextShared、ToList、SplitByDateTime；具体条件和先后次序见方法体。
        /// </summary>
        public virtual List<string> Query_Multi(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var strings = ReadAllTextShared(path);
            return strings.SplitByDateTime().ToList();
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query_MultiAsync。
        /// 正文中的关键协作调用包括 Combine、ReadAllTextSharedAsync、ToList、SplitByDateTime；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<List<string>> Query_MultiAsync(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var strings = await ReadAllTextSharedAsync(path);
            return strings.SplitByDateTime().ToList();
        }


        /// <summary>
        /// 执行查询操作；对应入口为 Query_Multi。
        /// 正文中的关键协作调用包括 QueryFunc、Query_Multi；具体条件和先后次序见方法体。
        /// </summary>
        public virtual List<string> Query_Multi(DateTime startTime, DateTime endTime)
        {
            return QueryFunc(startTime, endTime, x => Query_Multi(x));
        }

        /// <summary>
        /// 执行查询操作；对应入口为 Query_MultiAsync。
        /// 正文中的关键协作调用包括 QueryFuncAsync、Query_MultiAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task<List<string>> Query_MultiAsync(DateTime startTime, DateTime endTime)
        {
            return await QueryFuncAsync(startTime, endTime, x => Query_MultiAsync(x));
        }

        /// <summary>
        /// 判断时间间隔
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        private TimeSpan JudgeDatetime(DateTime startTime, DateTime endTime)
        {
            if (endTime.Day == startTime.Day) endTime = startTime.AddDays(1);
            return endTime - startTime;
        }

        /// <summary>
        /// 共享读（P0-3）：Serilog 常驻 logger 会一直占用文件句柄，
        /// 必须用 FileShare.ReadWrite 打开，否则 File.ReadAllText 会报 IOException 文件被占用。
        /// </summary>
        protected static string ReadAllTextShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// 共享读的真异步版（P5-1：useAsync 实现零线程池占用的异步文件 IO）。
        /// </summary>
        protected static async Task<string> ReadAllTextSharedAsync(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            using var sr = new StreamReader(fs);
            return await sr.ReadToEndAsync();
        }

       
    }
}
