// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ReportInfo、ReportID。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Buffers、Extension.IO.Compression、Prism.Ioc、SecsGem.Runtime.Variable.Data。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Prism.Ioc;
using SecsGem.Runtime.Variable.Data;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// Report数据结构
    /// </summary>
    public class ReportInfo
    {
        /// <summary>
        /// Report ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
 
        /// <summary>
        /// VID ID集合
        /// </summary>
        [NotMapped]
        internal List<string> VIDID { get; set; }

        
        /// <summary>
        /// 显示VID 名称集合
        /// </summary>
        public string VID_Collection
        {
            get => string.Join(",", VIDID);
            set => VIDID = value.Split(',').ToList();
        }
        /// <summary>
        /// VID信息集合
        /// </summary>
        public List<VIDInfo> VIDInfo;
    }

    /// <summary>
    /// ReportID 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：DataBase&lt;ReportInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ReportID : DataBase<ReportInfo>
    {
        public override string FilePath { get; set; } = "Config//SecsJson//ReportID";
        public override string FileName { get; set; } = "ReportID.csv";

        /// <summary>
        /// 构造函数
        /// </summary>
        public ReportID()
        {
        }

        /// <summary>
        /// 更新ReportID数据
        /// </summary>
        /// <param name="pVIDData">CVID数据集合</param>
        internal void Update(List<VIDInfo> pVIDData)
        {
            ///复制_ReportID
            List<ReportInfo> rptcopy = Datas.ToList();
            ///vid数据集合
            Dictionary<string, VIDInfo> viddata = pVIDData.ToDictionary(s => s.ID, s => s);

            foreach (ReportInfo r1 in rptcopy)
            {
                r1.VIDInfo = new List<VIDInfo>();
                List<string> vididlist = r1.VIDID;

                //更新VID
                for (int i = 0; i < vididlist.Count; i++)
                {
                    var key = vididlist[i];
                    VIDInfo vid; //更新数据
                    if (viddata.TryGetValue(key, out vid))
                        r1.VIDInfo.Add(vid);
                    else
                        throw new System.Exception("VID not exist in ReportID");
                }
            }
        }

        /// <summary>
        /// 修改ReportID
        /// </summary>
        /// <param name="pReportID">pReportID</param>
        /// <param name="pVIDID">需要修改的pVIDID</param>
        internal void Modify(string pReportID, List<string> pVIDID)
        {
            ReportInfo reportid = Datas.FirstOrDefault(x => x.ID == pReportID);

            //如果ReportID不存在
            if (reportid != null)
            {
                reportid.VIDID = pVIDID;
                Save();
            }
        }
    }
}