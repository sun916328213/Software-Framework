using DataManager.Excel;
using DataManager.Excel.Definition;
using DataManager.Excel.EPPlus;
using DataManager.Log.Definition;
using Extension.Core.CompareEx;
using Extension.Core.ConvertEx;
using Extension.Core.DataEx;
using Extension.Core.FileEx;
using Mapster;
using Prism.Ioc;
using Secs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using WPFControl.ProgramControl.Models;


namespace WPFControl.ProgramControl.Services
{
    public class AlarmInfoManagerService
    {
        internal AlarmInfoManagerModel Model { get; set; } = new AlarmInfoManagerModel();

        internal LogInfoService _LogInfoService;
        internal SecsControl _SecsControl;
        internal IExcel _ExcelManager;

        private string TotalAlarmTimeName = "总报警时间";
       
        /// <summary>
        /// 总报警时间计时器
        /// </summary>
        Stopwatch TotalAlarmStopwatch = new Stopwatch();
        /// <summary>
        /// 报警信息集合
        /// </summary>
        ConcurrentDictionary<int,AlarmInfo> AlarmInfos= new ConcurrentDictionary<int, AlarmInfo>();

        /// <summary>
        /// 报警信息文件路径
        /// </summary>
        public string FilePath { get; set; } = "D:\\DeviceData\\Alarm";
        /// <summary>
        /// 报警信息文件名
        /// </summary>
        public string FileName => DateTime.Now.ToDateNOSapce() + ".xlsx";
        /// <summary>
        /// 总报警时间
        /// </summary>
        public double TotalAlarmTime = 0;

        public AlarmInfoManagerService(IContainerProvider container)
        {
            _LogInfoService = container.Resolve<LogInfoService>();
            _SecsControl = container.Resolve<SecsControl>();

            _ExcelManager = container.Resolve< EPPlusHelper>();
            _ExcelManager.ExcelParam = new ExcelParam()
            {
                Name = "AlarmInfo",
                FilePath = FilePath,
                FileName = FileName,
                ToolType = ExcelType.EPPlus
            };
            Load();
        }

        public void Load()
        {
            string filePath = Model.FilePath;
            string fileName = Model.FileName;
            string path = Path.Combine(filePath, fileName);
            if (!File.Exists(path)) throw new Exception("File Not Exist!!");

            //读取CSV文件
            Model.DataCollection = new ObservableCollection<Models.AlarmInfoManager>(CSVEx.ReadCsv<Models.AlarmInfoManager>(path));
        }

        public async Task Save()
        {
            string filePath = Model.FilePath;
            string fileName = Model.FileName;
            string path = Path.Combine(filePath, fileName);
            await CSVEx.ExportToCsvAsync(Model.DataCollection, path);
        }

        public List<Models.AlarmInfoManager> GetAlarms() => Model.DataCollection.ToList();


        private short[] last = null;

        /// <summary>
        /// 组织报警信息
        /// </summary>
        /// <param name="data"></param>
        /// <param name="IsBit"></param>
        public void Organize(short[] data, bool IsBit = true)
        {
            if (last == null) last = data.DeepClone();
            //筛选出不相等的数据
            var d1 = BitOperationEx.Compare_Ptr(data, last, EComparePolarity.BothEdges);
            last = data.DeepClone();
            if (d1 == null || d1.Count == 0) return;

            //遍历数据 
            foreach (var item in d1)
            {
                //将short[]转成bit[]
                var bits = data[item.Index].ToBoolArray();

                for (int Index = 0; Index < bits.Length; Index++)
                {
                    var curbit = bits[Index];
                    string add = item.Index + "." + Index;
                    var finddata = Model.DataCollection.FirstOrDefault(x => x.Address.Contains(add));

                    if (finddata == null) return;
                    if (curbit == true) //检测到报警
                    {
                        TriggerAlarm(finddata,true);
                    }
                    else  //报警恢复
                    {
                        ResetAlarm(finddata, true);
                    }
                }
            }
        }



        /// <summary>
        /// 触发报警
        /// </summary>
        /// <param name="alarmInfo">报警信息</param>
        /// <param name="islog">是否记录Log</param>
        public void TriggerAlarm(Models.AlarmInfoManager alarmInfo, bool islog=false)
        {
            AlarmInfo alarm = new AlarmInfo()
            {
                ID = alarmInfo.ID,
                Info = alarmInfo.Info_CH,
                StartTime = DateTime.Now,
            };

            if (AlarmInfos.TryAdd(alarmInfo.ID, alarm))
            {
                alarm.Stopwatch.Restart();
                if (AlarmInfos.Count > 0)
                    TotalAlarmStopwatch.Start();

                if (islog)
                    //添加运行日志
                    _LogInfoService.AddRunInfo(alarmInfo.Info_CH, ELogLevel.Warning);

                //写入Excel报警信息
                _ExcelManager.Add(alarm.Adapt<AlarmInfoDTO>());

                if (alarmInfo.Enable)
                {
                    //发送EAP
                    Secs.Variable.AlarmInfo alarmInfo1 = new Secs.Variable.AlarmInfo()
                    {
                        ID = alarmInfo.ID.ToString(),
                        Description = alarmInfo.Info_EN,
                        Code = 1,
                    };
                    _SecsControl._VIDManager.UpdateAlarm(alarmInfo1);
                }
            }
        }

        /// <summary>
        /// 重置报警
        /// </summary>
        /// <param name="alarmInfo"></param>
        public void  ResetAlarm(Models.AlarmInfoManager alarmInfo,bool islog=false)
        {
            if (alarmInfo == null) return;
            if (AlarmInfos.TryRemove(alarmInfo.ID, out var alarm))
            {
                alarm.Stopwatch.Stop();

                if (AlarmInfos.Count == 0)
                {
                    TotalAlarmStopwatch.Stop();
                }

                if (islog)
                {
                    //添加运行日志
                    _LogInfoService.AddRunInfo($"{alarmInfo.Info_CH} 复位  持续时间:{alarm.Stopwatch.ElapsedMilliseconds} ms", ELogLevel.Reset);
                }


                //更新Excel报警信息
                alarm.EndTime = DateTime.Now ;
                alarm.Duration =  (alarm.Stopwatch.ElapsedMilliseconds/1000.0);
                var update = alarm.Adapt<AlarmInfoDTO>();

                AlarmInfo alarmInfo2=new AlarmInfo(); 
                 update.Adapt (alarmInfo2);
                _ExcelManager.Update(x =>x.ID==alarmInfo.ID&& x.Adapt<AlarmInfo>().Duration==0, update);

                if (alarmInfo.Enable)
                {
                    //发送EAP
                    Secs.Variable.AlarmInfo alarmInfo1 = new Secs.Variable.AlarmInfo()
                    {
                        ID = alarmInfo.ID.ToString(),
                        Description = alarmInfo.Info_EN,
                        Code = 0,
                    };
                    _SecsControl._VIDManager.UpdateAlarm(alarmInfo1);
                }
            }
        }
 
       
    }
}
