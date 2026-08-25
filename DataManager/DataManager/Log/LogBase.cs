using Extension.Struct;
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

        public async Task AddAsync(string info, ELogLevel logLevel = ELogLevel.Information)
        {
             await Task.Run(() => Add(info, logLevel));
        }

        public virtual List<LogInfo> Query(string fileName)
        {
            List<LogInfo> logInfos = new List<LogInfo>();
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var datas = System.IO.File.ReadAllText(path);
            var dataselect = datas.SplitByDateTime().Select(s => s.Split(Param.Separator.ToCharArray()).Where(x => x != string.Empty));
            foreach (var info in dataselect)
            {
                if (info.Count() >= 3)
                    logInfos.Add(new LogInfo(info.ElementAt(0), info.ElementAt(2).Trim(), (ELogLevel)Enum.Parse(typeof(ELogLevel), info.ElementAt(1))));
                else
                    logInfos.Add(new LogInfo() { Info = info.ElementAt(0) });
            }
            return logInfos;
        }
        public async Task<List<LogInfo>> QueryAsync(string fileName)
        {
           return await Task.Run(() => Query(fileName));
        }

        private List<T> QueryFunc<T>(DateTime startTime, DateTime endTime,Func<string ,List<T>> func)
        {
            List<T> logInfos = new List<T>();
            if (endTime < startTime) throw new Exception("The start time is greater than the end time!!");

            TimeSpan timeSpan = JudgeDatetime(startTime, endTime);
            for (int index = 0; index < timeSpan.Days; index++)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(Param.FilePath);
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
            return await Task.Run(() => Query(startTime, endTime));
        }


        public virtual List<string> Query_Multi(string fileName)
        {
            string path = System.IO.Path.Combine(Param.FilePath, fileName);
            var strings = System.IO.File.ReadAllText(path);
            return strings.SplitByDateTime().ToList();
        }

        public async Task<List<string>> Query_MultiAsync(string fileName)
        {
            return await Task.Run(() => Query_Multi(fileName));
        }


        public virtual List<string> Query_Multi(DateTime startTime, DateTime endTime)
        {
            return QueryFunc(startTime, endTime, x => Query_Multi(x));
        }

        public async Task<List<string>> Query_MultiAsync(DateTime startTime, DateTime endTime)
        {
            return await Task.Run(() => Query_Multi(startTime, endTime));
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

       
    }
}
