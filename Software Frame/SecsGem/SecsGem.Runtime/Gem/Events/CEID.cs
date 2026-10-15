// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：CEIDInfo、CEID。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Buffers、Extension.IO.Compression、Extension.Core.Collections、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Core.Collections;
using Prism.Ioc;
using SecsGem.Runtime.Variable.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// CEID数据结构
    /// </summary>
    public class CEIDInfo
    {
        /// <summary>
        /// CEID 唯一标识符
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// CEID 名称
        /// </summary>
        public string Name { get; set; }
 
        /// <summary>
        /// Report 报表信息集合
        /// </summary>
        internal List<ReportInfo> ReportInfo;
        /// <summary>
        /// Report 名称集合
        /// </summary>
        [NotMapped]
        internal List<string> ReportID { get; set; } = new();

        /// <summary>
        /// 显示Report 名称集合
        /// </summary>
        public string ReportID_Collection
        {
            // 同 ReportInfo.VID_Collection：空事件合法，Join(null) 会炸新增/删除时的全表映射。
            get => ReportID == null ? string.Empty : string.Join(",", ReportID);
            set => ReportID = string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }
        /// <summary>
        /// CEID 启用状态
        /// </summary>
        public bool Enable { get; set; } = true;
    }
    /// <summary>
    /// CEID 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：DataBase&lt;CEIDInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class CEID : DataBase<CEIDInfo>
    {
        public override string FilePath { get; set; } = "Config//SecsJson//CEID";
        public override string FileName { get; set; } = "CEID.csv";
        public override ConcurrentList<CEIDInfo> Datas { get; set; } = new ConcurrentList<CEIDInfo>();

        /// <summary>
        /// 构造函数
        /// </summary>
        public CEID( )
        {
        }

        /// <summary>
        /// 缺 CSV 时的打底事件：控制状态那五条。
        /// 五条全挂 DeviceMode/DeviceStatus 报表，事件自带状态值，Host 才拿得到新模式。
        /// </summary>
        protected override void LoadDefaults()
        {
            foreach (var e in ProtectedItems()) Datas.Add(e);
        }

        /// <summary>
        /// 受保护的常用事件：界面上删掉后，下次 <c>Load</c> 按 ID 补回来。
        /// </summary>
        protected override void EnsureDefaults()
        {
            foreach (var e in ProtectedItems())
            {
                if (Datas.All(x => x.ID != e.ID))
                {
                    Datas.Add(e);
                    System.Diagnostics.Debug.WriteLine($"CEID 缺常用项 {e.ID}_{e.Name}，已补回");
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<CEIDInfo> ProtectedItems()
        {
            yield return new CEIDInfo { ID = "101", Name = "Device Status", ReportID_Collection = "1,13,12" };
            yield return new CEIDInfo { ID = "102", Name = "Device Mode", ReportID_Collection = "1,13,11" };
            yield return new CEIDInfo { ID = "103", Name = "Device Online", ReportID_Collection = "1,13,11" };
            yield return new CEIDInfo { ID = "104", Name = "Device Offline", ReportID_Collection = "1,13,11" };
            yield return new CEIDInfo { ID = "105", Name = "Device Local", ReportID_Collection = "1,13,11" };
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="pReportData"></param>
        internal void Update(List<ReportInfo> pReportData)
        {
            ///复制report集合
            List<CEIDInfo> ceidcopy = Datas.ToList();

            Dictionary<string, ReportInfo> rptdata = pReportData.ToDictionary(s => s.ID, s => s);

            foreach (CEIDInfo r1 in ceidcopy)
            {
                r1.ReportInfo = new List<ReportInfo>();
                List<string> rptnamelist = r1.ReportID;

                //更新VID集合 reportid
                for (int i = 0; i < rptnamelist.Count; i++)
                {
                    string key = rptnamelist[i];
                    ReportInfo rpt;
                    if (rptdata.TryGetValue(key, out rpt)) //更新数据
                        r1.ReportInfo.Add(rpt);
                    else
                        throw new System.Exception("ReportID not exist in CEID");
                }
            }
        }


        /// <summary>
        /// 修改ReportID
        /// </summary>
        /// <param name="pCEID">event ID</param>
        /// <param name="pReportID">repoet id</param>
        /// <returns></returns>
        internal void Modify(string pCEID, List<string> pReportID)
        {
            try
            {
                var data = Datas.FirstOrDefault (x=>x.ID == pCEID);
                if (data == null) throw new Exception("Event ID NoExist!!");

                // 去重保序，同 ReportID.Modify：Host 重复点同一个报表只挂一份。
                data.ReportID = Dedup(pReportID); // /刷新ReportID挂载VID格式
                Save();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + ex.StackTrace);
            }
        }



        /// <summary>
        /// 获取集合数据 字典格式 Key=ID_Name
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, CEIDInfo> GetDataDic_IDAndName()
        {
            try
            {
                Dictionary<string, CEIDInfo> copy = Datas.ToDictionary(s => s.ID + "_" + s.Name, s => s);
                return copy;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + ex.StackTrace);
            }
        }

        /// <summary>
        /// 根据名字获取CCEID
        /// </summary>
        /// <param name="pName"></param>
        /// <returns></returns>
        public CEIDInfo this[string pName]
        {
            get
            {
                try
                {
                    return Datas.FirstOrDefault(s => s.Name == pName);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message + ex.StackTrace);
                }
            }
        }
    }
}