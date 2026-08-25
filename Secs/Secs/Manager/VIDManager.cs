using Extension.Tool;
using Prism.Ioc;
using Secs.Variable;
using Secs.Variable.Data;
using Secs4Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Secs.Manager
{
    public class VIDManager:  ManagerBase
    {
        public ReportID ReportID  ;
        public CEID  CEID;
        public DVID DVID  ;
        public SVID SVID ;
        public ECID ECID;

        public Alarm Alarm ;
        public ActiveReceive ActiveReceive;

       public VIDManager(IContainerProvider container)  :base(container)
        {
            ReportID = container.Resolve<ReportID>();
            CEID = container.Resolve<CEID>();
            DVID = container.Resolve<DVID>();
            SVID = container.Resolve<SVID>();
            ECID = container.Resolve<ECID>();

            Alarm= container.Resolve<Alarm>();
            ActiveReceive = container.Resolve<ActiveReceive>();
        }

        /// <summary>
        /// 所有VID集合
        /// </summary>
        public ConcurrentList<VIDInfo> VIDList { get; private set; } = new ConcurrentList<VIDInfo>();

        /// <summary>
        /// 所有VID字典集合 Key=VID.Name
        /// </summary>
        public Dictionary<string, VIDInfo> VIDDic_Name { get; private set; } = new Dictionary<string, VIDInfo>();

        /// <summary>
        /// 所有VID字典集合 Key=VID.ID
        /// </summary>
        public Dictionary<string, VIDInfo> VIDDic_ID { get; private set; } = new Dictionary<string, VIDInfo>();

        public void Load()
        {
            VIDList.Clear();
            DVID.Load();
            SVID.Load();
            ECID.Load();
            VIDList.AddRange(DVID.GetDatas());
            VIDList.AddRange(SVID.GetDatas());
            VIDList.AddRange(ECID.GetDatas());

            // 使用容量预分配减少内存重新分配
            VIDDic_Name = new Dictionary<string, VIDInfo>(VIDList.Count);
            VIDDic_ID = new Dictionary<string, VIDInfo>(VIDList.Count);

            foreach (var vid in VIDList)
            {
                VIDDic_Name[vid.Name] = vid;
                VIDDic_ID[vid.ID] = vid;
            }

            ReportID.Load();
            CEID.Load();
            Alarm.Load();
        }


        /// <summary>
        /// 更新VID数据
        /// </summary>
        /// <param name="pName">Name</param>
        /// <param name="pValue">数据</param>
        /// <returns></returns>
        public void UpdateVID(string pName, object pValue)
        {
            if (string.IsNullOrEmpty(pName)) return;
            
            if (VIDDic_Name.TryGetValue(pName, out VIDInfo vid))
            {
                vid.Value = pValue;
            }
            else
                throw new Exception($"{pName} Not Exist VID");
        }

        /// <summary>
        /// 追加VID数据：列表型VID在原集合后追加一项；
        /// 修复：原实现把任意 IList 强转为 List&lt;object&gt;（如 List&lt;string&gt; 会抛 InvalidCastException）。
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        /// <exception cref="Exception"></exception>
        public void AppendVID(string pName, object pValue)
        {
            if (string.IsNullOrEmpty(pName)) return;

            if (VIDDic_Name.TryGetValue(pName, out VIDInfo vid))
            {
                List<object> items = new List<object>();

                switch (vid.Value)
                {
                    case List<object> list:
                        items.AddRange(list);
                        break;
                    case IEnumerable enumerable when !(vid.Value is string):
                        foreach (var item in enumerable)
                        {
                            items.Add(item);
                        }
                        break;
                    case null:
                        break;
                    default:
                        // 原值是标量：保留为第一项再追加，避免数据丢失
                        items.Add(vid.Value);
                        break;
                }

                items.Add(pValue);
                vid.Value = items;
            }
            else
                throw new Exception($"{pName} Not Exist VID");
        }

        /// <summary>
        /// 更新VID数据
        /// </summary>
        /// <param name="pVID">VID集合</param>
        /// <returns></returns>
        public void UpdateVID(List<VIDInfo> pVID)
        {
            if (pVID == null) return;
            
            // 使用for循环而不是ElementAt提高性能
            for (int index = 0; index < pVID.Count; index++)
            {
                var vidItem = pVID[index];
                if (VIDDic_Name.TryGetValue(vidItem.Name, out VIDInfo vid))
                {
                    vid.Value = vidItem.Value;
                }
            }
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="pVID">字典集合 Key=Name Value=值</param>
        /// <returns></returns>
        public void UpdateVID(Dictionary<string, object> pVID)
        {
            if (pVID == null) return;
            
            // 使用foreach而不是ElementAt提高性能
            foreach (var kvp in pVID)
            {
                if (VIDDic_Name.TryGetValue(kvp.Key, out VIDInfo vid))
                {
                    vid.Value = kvp.Value;
                }
            }
        }

        /// <summary>
        /// 更新CEID数据
        /// </summary>
        internal void UpdateCEID()
        {
            ///更新数据ReportID
           ReportID.Update(VIDList.ToList());

            ///更新数据CEID
           CEID.Update( ReportID.GetDatas());
        }


        /// <summary>
        /// 添加报警信息 已经存在则不添加
        /// </summary>
        /// <param name="pAlarms"></param>
        public void AddAlarm(List<AlarmInfo> pAlarms)
        {
            if (pAlarms == null) return;
            Alarm.AddCollect(pAlarms);
        }


        /// <summary>
        /// 更新报警信息 单个更新
        /// </summary>
        /// <param name="pAlarms"></param>
        public async void UpdateAlarm(AlarmInfo pAlarms)
        {
            if (pAlarms == null) return;
            Alarm.Update(pAlarms);
            await SecsControl.SendItem("S5F1", true, pAlarms);
        }

 
        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            DVID.Save();
            SVID.Save();
            ECID.Save();

            ReportID.Save();
            CEID.Save();
        }
 

        /// <summary>
        /// 获取VID 根据ID
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public VIDInfo GetVIDByID(string ID)
        {
            if (string.IsNullOrEmpty(ID)) return null;
            VIDDic_ID.TryGetValue(ID, out VIDInfo vID);
            return vID;
        }

        /// <summary>
        /// 获取VID 根据Name
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public VIDInfo GetVIDByName(string Name)
        {
            if (string.IsNullOrEmpty(Name)) return null;
            VIDDic_Name.TryGetValue(Name, out VIDInfo vID);
            return vID;
        }

        /// <summary>
        /// 获取ID_Name
        /// </summary>
        /// <returns></returns>
        public List<string> GetID_Name()
        {
            // 使用Capacity预分配以减少内存重新分配
            List<string> result = new List<string>(VIDList.Count);
            foreach (var vid in VIDList)
            {
                result.Add($"{vid.ID}_{vid.Name}");
            }
            return result;
        }
 
        /// <summary>
        /// 根据ID获取值 
        /// </summary>
        /// <param name="id">ID</param>
        /// <returns></returns>
        public string GetValueById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            
            if (VIDDic_ID.TryGetValue(id, out VIDInfo vID))
            {
                return vID.Value?.ToString();
            }
            return null;
        }

        /// <summary>
        /// 根据名称获取值  
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public string GetValueByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            
            if (VIDDic_Name.TryGetValue(name, out VIDInfo vID))
            {
                return vID.Value?.ToString();
            }
            return null;
        }
 
    }
}