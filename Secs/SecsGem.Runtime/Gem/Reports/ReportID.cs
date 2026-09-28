using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Prism.Ioc;
using Secs.Variable.Data;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Secs.Variable
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