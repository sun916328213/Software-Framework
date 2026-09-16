using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Extension.Core.ConvertEx;
using DataManager.Log.Definition;
using Extension.Core.FileEx;

namespace DataManager.Log
{
    public abstract class LogBase : ILog
    {
        public abstract LogParam  Param { get; set; }
 
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


        public virtual List<LogInfo> Query(DateTime startTime, DateTime endTime)
        {
            return QueryFunc(startTime, endTime, x => Query(x));
        }




        public async Task<List<LogInfo>> QueryAsync(DateTime startTime, DateTime endTime)
        {
            return await QueryFuncAsync(startTime, endTime, x => QueryAsync(x));
        }

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


        public virtual List<string> Query_Multi(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var strings = ReadAllTextShared(path);
            return strings.SplitByDateTime().ToList();
        }

        public async Task<List<string>> Query_MultiAsync(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var strings = await ReadAllTextSharedAsync(path);
            return strings.SplitByDateTime().ToList();
        }


        public virtual List<string> Query_Multi(DateTime startTime, DateTime endTime)
        {
            return QueryFunc(startTime, endTime, x => Query_Multi(x));
        }

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
