// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoManagerService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Excel、DataManager.Runtime.Excel、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Excel;
using DataManager.Runtime.Excel;
using DataManager.Core.Excel.Definition;
using DataManager.Runtime.Excel.EPPlus;
using DataManager.Core.Log.Definition;
using Extension.Core.Conversion;
using Extension.Core.Comparison;
using Extension.Integration.Cloning;
using Extension.IO.Csv;
using Mapster;
using Prism.Ioc;
using SecsGem.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using WPFControls.ProgramControl.Models;


namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// AlarmInfoManagerService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class AlarmInfoManagerService
    {
        internal AlarmInfoManagerModel Model { get; set; } = new AlarmInfoManagerModel();

        internal LogInfoService _LogInfoService;
        internal ISecsGemRuntime _secsRuntime;
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

        /// <summary>
        /// 初始化 AlarmInfoManagerService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、Load；具体条件和先后次序见方法体。
        /// </summary>
        public AlarmInfoManagerService(IContainerProvider container)
        {
            _LogInfoService = container.Resolve<LogInfoService>();
            _secsRuntime = container.Resolve<ISecsGemRuntime>();

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

        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 Combine、Exists、ReadCsv；具体条件和先后次序见方法体。
        /// </summary>
        public void Load()
        {
            string filePath = Model.FilePath;
            string fileName = Model.FileName;
            string path = Path.Combine(filePath, fileName);
            if (!File.Exists(path)) throw new Exception("File Not Exist!!");

            //读取CSV文件
            Model.DataCollection = new ObservableCollection<Models.AlarmInfoManager>(CSVEx.ReadCsv<Models.AlarmInfoManager>(path));
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 Combine、ExportToCsvAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public async Task Save()
        {
            string filePath = Model.FilePath;
            string fileName = Model.FileName;
            string path = Path.Combine(filePath, fileName);
            await CSVEx.ExportToCsvAsync(Model.DataCollection, path);
        }

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetAlarms。
        /// 正文中的关键协作调用包括 ToList；具体条件和先后次序见方法体。
        /// </summary>
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
                    _ = _secsRuntime.ReportAlarmAsync(new SecsGemAlarm(
                        alarmInfo.ID.ToString(), alarmInfo.Info_EN, 1));
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
                    _ = _secsRuntime.ReportAlarmAsync(new SecsGemAlarm(
                        alarmInfo.ID.ToString(), alarmInfo.Info_EN, 0));
                }
            }
        }


    }
}
