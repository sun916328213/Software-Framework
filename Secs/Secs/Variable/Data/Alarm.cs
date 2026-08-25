using Extension.Tool;
using Prism.Ioc;
using Secs.Variable.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Variable
{ 
    /// <summary>
   /// 报警信息
   /// </summary>
    public class AlarmInfo
    {
        /// <summary>
        /// 报警状态 1=报警 0=复位
        /// </summary>
        public byte Code { get; set; }
        /// <summary>
        /// 报警ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// 报警描述 English
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enable { get; set; } = true;
    }
    public class Alarm : DataBase<AlarmInfo> 
    {
        public Alarm()
        {
        }

        public override string FilePath { get; set; } = "Config//SecsJson//Alarm";
        public override string FileName { get; set; } = "Alarm.csv";
        public override ConcurrentList<AlarmInfo> Datas { get; set; }=new ConcurrentList<AlarmInfo>();
 
        /// <summary>
        /// 添加报警集合 已经存在则不添加
        /// </summary>
        /// <param name="pAlarm">报警信息列表</param>
        public void AddCollect(List<AlarmInfo> pAlarm)
        {
            foreach (AlarmInfo item in pAlarm)
            {
                /// 判断ID是否已存在
                if(!Datas.Where(x=>x.ID==item.ID).Any())
                {
                    Datas.Add(item);
                }
            }
            Save();
        }

        /// <summary>
        /// 更新报警数据 单个报警 只需要ID和Code 其余数据不做更新
        /// </summary>
        /// <param name="pAlarm">只需要ID和Code 其余数据不做更新</param>
        public void Update(AlarmInfo pAlarm)
        {
            var alarmInfo = Datas.First(x => x.ID == pAlarm.ID);
            alarmInfo.Code = pAlarm.Code; // 只需要更新状态即可
        }

        /// <summary>
        /// 更新报警数据 只需要ID和Code 其余数据不做更新
        /// </summary>
        /// <param name="pAlarm">只需要ID和Code 其余数据不做更新</param>
        public void Update(List<AlarmInfo> pAlarm)
        { 
            var datas=Datas.ToDictionary(x=>x.ID,x=>x);
            foreach (AlarmInfo item in pAlarm)
            {
                AlarmInfo alarmInfo;
                if (datas.TryGetValue(item.ID, out alarmInfo))
                {
                    alarmInfo.Code = item.Code; // 只需要更新状态即可
                }
            }
        }

      

        /// <summary>
        /// 设置报警启用信息 
        /// </summary>
        /// <param name="id">报警ID</param>
        /// <param name="enable">是否启用</param>
        public void SetEnable(string id, bool enable)
        {
            var datas = Datas.ToDictionary(x => x.ID, x => x);
            AlarmInfo alarmInfo;
            if (datas.TryGetValue(id, out alarmInfo))
            {
                alarmInfo.Enable = enable;
            }
        }
    }
}
