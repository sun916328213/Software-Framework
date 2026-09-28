using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Extension.Core.Collections;
using Prism.Ioc;
using Secs.Variable.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Secs.Variable
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
        internal List<string> ReportID { get; set; }

        /// <summary>
        /// 显示Report 名称集合
        /// </summary>
        public string ReportID_Collection
        {
            get => string.Join(",", ReportID);
            set => ReportID = value?.Split(',').ToList() ?? new List<string>();
        }
        /// <summary>
        /// CEID 启用状态
        /// </summary>
        public bool Enable { get; set; } = true;
    }
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

                data.ReportID = pReportID ?? new List<string>(); // /刷新ReportID挂载VID格式
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