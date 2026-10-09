// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoManagerService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Excel、DataManager.Runtime.Excel、DataManager.Core.Excel.Definition、DataManager.Runtime.Excel.EPPlus、DataManager.Core.Log.Definition、Extension.Core.Conversion、Extension.Core.Comparison、Extension.Integration.Cloning、Extension.IO.Csv、Mapster、Prism.Ioc、SecsGem.Core。导入不代表所有类型都会在运行时加载。
//
// 【T12b：报警表的持久化归属】
// 报警表（Config\Alarm.csv）的**读写都归本服务**：Load 读盘、Save 校验后落盘。
// 改造前保存按钮由 ConfigManagerView.SaveData 先 JsonEx.Save 往 Alarm.csv 写一份 **JSON**，
// 再在 try/catch 之外调 SaveCallback → 本服务 Save() 用 CSV 覆盖回去：
// ① 写盘窗口期文件内容是 JSON，进程若在此刻退出，下次 Load 拿 CSVEx.ReadCsv 读 JSON；
// ② CSV 覆盖失败时文件就永久停在 JSON 上；③ 该回调是 async-void lambda，写盘失败直接崩进程。
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
        /// 最近一次 <see cref="Load"/> 的结果说明。正常读到时为空串；缺文件 / 读失败时说明原因与后果，
        /// 供页面摆在状态行上 —— 「报警表是空的，但没人知道为什么」是最糟的状态。
        /// </summary>
        internal string LoadMessage { get; private set; } = string.Empty;

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
        /// 从 <see cref="AlarmInfoManagerModel.FilePath"/> + <c>FileName</c> 指定的 CSV 读取报警表。
        ///
        /// <para>
        /// <b>本方法不抛异常。</b>缺文件、路径没配、内容损坏一律退化成「空表 + <see cref="LoadMessage"/> 说明」。
        /// 理由是调用链上没有任何一处能承接构造期异常：本方法由构造函数调用，而构造函数由
        /// <c>AutoProcess</c>（生产流程启动）与 <c>LogAlarmInfoService</c>（每条报警日志）触发 ——
        /// 一个抛在构造期的裸 <c>Exception</c> 会让整条生产流程起不来，且报错信息只有一句 "File Not Exist!!"。
        /// 降级为空表至少让界面打得开、<see cref="Organize"/> / <c>TriggerAlarm</c> 不再 NRE。
        /// </para>
        ///
        /// <para>
        /// <see cref="AlarmInfoManagerModel.DataCollection"/> 在**任何**返回路径上都非 null：
        /// 它是 <c>Organize</c> 与 <c>LogAlarmInfoService</c> 直接 <c>FirstOrDefault</c> 的对象。
        /// </para>
        /// </summary>
        public void Load()
        {
            Model.DataCollection = new ObservableCollection<Models.AlarmInfoManager>();
            LoadMessage = string.Empty;

            string path = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(Model.FilePath) || string.IsNullOrEmpty(Model.FileName))
                {
                    LoadMessage = "报警配置未设置文件路径(FilePath)或文件名(FileName)，已按空表启动。";
                    return;
                }

                path = Path.Combine(Model.FilePath, Model.FileName);
                if (!File.Exists(path))
                {
                    LoadMessage = $"未找到报警配置文件 {path}，已按空表启动：不会识别任何报警。";
                    return;
                }

                // 读取CSV文件（CSVEx.ReadCsv 把一切失败包成裸 Exception，所以先自己判存在）
                Model.DataCollection = new ObservableCollection<Models.AlarmInfoManager>(
                    CSVEx.ReadCsv<Models.AlarmInfoManager>(path));
            }
            catch (Exception ex)
            {
                Model.DataCollection = new ObservableCollection<Models.AlarmInfoManager>();
                LoadMessage = $"报警配置文件 {path} 读取失败，已按空表启动：{ex.Message}";
            }
        }

        /// <summary>
        /// 把 <see cref="AlarmInfoManagerModel"/> 里正在编辑的报警表**落盘**（校验 → 写文件）。
        /// 任一步失败都直接抛出且**不**执行后续步骤：校验失败 ⇒ 不写盘；写盘失败 ⇒ 磁盘保持原样
        /// （<see cref="CSVEx.ExportToCsvAsync"/> 是 <c>FileMode.Create</c> + <c>FileShare.None</c>，
        /// 打不开就抛在截断之前；被 Excel 占住是现场最常见的一种）。
        ///
        /// <para>
        /// 本服务的「生效」就是 <see cref="AlarmInfoManagerModel.DataCollection"/> 本身 ——
        /// <c>Organize</c> / <c>LogAlarmInfoService</c> 直接读它，没有第二份运行态副本，
        /// 所以落盘成功即生效，失败时内存里的编辑内容原样保留。
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">某行的寄存器地址为空（读回来是 null，会让 <see cref="Organize"/> 抛 NRE）。</exception>
        /// <exception cref="InvalidOperationException">报警ID 重复（会被 <c>AlarmInfos.TryAdd</c> 静默丢弃），或路径 / 文件名未设置。</exception>
        public async Task Save()
        {
            var rows = (Model.DataCollection ?? new ObservableCollection<Models.AlarmInfoManager>()).ToList();

            // ① 先校验：坏配置绝不写盘，也就不会被下次启动加载进来。
            Validate(rows);

            if (string.IsNullOrEmpty(Model.FilePath) || string.IsNullOrEmpty(Model.FileName))
                throw new InvalidOperationException("请先设置报警配置的文件路径(FilePath)和文件名称(FileName)。");

            // ② 再落盘：目录不存在时由 CSVEx.ExportToCsvAsync 补建。写盘失败按原样抛出，不吞。
            string path = Path.Combine(Model.FilePath, Model.FileName);
            await CSVEx.ExportToCsvAsync(rows, path).ConfigureAwait(false);
        }

        /// <summary>
        /// 校验一份**即将写盘**的报警表，只钉两条会真的造成运行期故障的不变式：
        /// <list type="bullet">
        /// <item>地址为空 ⇒ CSV 里是空字段 ⇒ 读回来 <c>Address</c> 是 null ⇒
        /// <see cref="Organize"/> 的 <c>x.Address.Contains(...)</c> 抛 <see cref="NullReferenceException"/>，
        /// 报警扫描线程直接死；</item>
        /// <item>报警ID 重复 ⇒ <c>AlarmInfos.TryAdd</c> 返回 false ⇒ 第二条报警**永远发不出去**
        /// （半导体设备上这是安全问题，且完全静默）。</item>
        /// </list>
        /// 其余字段（中文描述、英文描述、Enable）留空只是「这条报警匹配不上」，不构成故障，故不拦。
        /// </summary>
        private static void Validate(IReadOnlyList<Models.AlarmInfoManager> rows)
        {
            var ids = new HashSet<int>();
            foreach (var row in rows)
            {
                if (row == null)
                    throw new ArgumentException("报警配置包含空行。");

                if (string.IsNullOrWhiteSpace(row.Address))
                    throw new ArgumentException($"报警项 {row.ID} 的寄存器地址(Address)为空。");

                if (!ids.Add(row.ID))
                    throw new InvalidOperationException($"报警配置包含重复的报警ID {row.ID}。");
            }
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
