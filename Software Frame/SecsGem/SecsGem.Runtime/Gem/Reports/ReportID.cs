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
        internal List<string> VIDID { get; set; } = new();

        
        /// <summary>
        /// 显示VID 名称集合
        /// </summary>
        public string VID_Collection
        {
            // 空报表是合法的（CSV 里空格子会被 ReadCsv 跳过，VIDID 就是空列表而不是 null）；
            // 原来这里 Join(null) 直接抛 "Value cannot be null"，新增/删除时全表映射扫到这一行就炸。
            get => VIDID == null ? string.Empty : string.Join(",", VIDID);
            set => VIDID = string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
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
        /// 缺 CSV 时的打底报表，引用写裸 ID，与 SVID/ECID 的默认项对得上：
        /// Clock / DeviceMode / DeviceStatus / DeviceInfo(设备名+版本)。
        /// </summary>
        protected override void LoadDefaults()
        {
            foreach (var r in ProtectedItems()) Datas.Add(r);
        }

        /// <summary>
        /// 受保护的常用报表：界面上删掉后，下次 <c>Load</c> 按 ID 补回来。
        /// </summary>
        protected override void EnsureDefaults()
        {
            foreach (var r in ProtectedItems())
            {
                if (Datas.All(x => x.ID != r.ID))
                {
                    Datas.Add(r);
                    System.Diagnostics.Debug.WriteLine($"ReportID 缺常用项 {r.ID}_{r.Name}，已补回");
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<ReportInfo> ProtectedItems()
        {
            yield return new ReportInfo { ID = "1", Name = "Clock", VID_Collection = "101" };
            yield return new ReportInfo { ID = "11", Name = "DeviceMode", VID_Collection = "102" };
            yield return new ReportInfo { ID = "12", Name = "DeviceStatus", VID_Collection = "103" };
            yield return new ReportInfo { ID = "13", Name = "DeviceInfo", VID_Collection = "104,105" };
        }

        /// <summary>
        /// 更新ReportID数据
        /// </summary>
        /// <param name="pVIDData">CVID数据集合</param>
        internal void Update(List<VIDInfo> pVIDData)
        {
            ///复制_ReportID
            List<ReportInfo> rptcopy = Datas.ToList();
            ///vid数据集合。VID 的 ID 跨表唯一（VIDManager.Load 会校验），所以键就是裸 ID。
            ///配置里写成 "SVID:102" 也能认，NormalizeId 会剥掉前缀。
            Dictionary<string, VIDInfo> viddata = new Dictionary<string, VIDInfo>(pVIDData.Count);
            foreach (var v in pVIDData)
                viddata[VIDInfo.NormalizeId(v.ID)] = v;

            foreach (ReportInfo r1 in rptcopy)
            {
                r1.VIDInfo = new List<VIDInfo>();
                List<string> vididlist = r1.VIDID;

                //更新VID
                for (int i = 0; i < vididlist.Count; i++)
                {
                    var key = VIDInfo.NormalizeId(vididlist[i] ?? string.Empty);
                    if (key.Length == 0) continue;

                    if (!viddata.TryGetValue(key, out var vid))
                        throw new Exception($"VID not exist in ReportID: {vididlist[i]}");

                    r1.VIDInfo.Add(vid);
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

            // 报表不存在必须报错，不能静默跳过再回 Success：
            // Host 会以为这次链接成功了，实际数据仍旧挂在原来那个报表下面
            //（现场表现：第二条链接请求"没生效"，且所有变量看起来都归第一个报表）。
            // 抛出去由 S2F33 转成 S9F7，Host 立刻知道该 RPTID 不存在。
            if (reportid == null)
                throw new Exception($"Report ID No Exist: {pReportID}");

            // 去重保序：Host 重复点同一个 VID 时，报表里只留一份，
            // 否则 S6F11 会把同一个值连发两遍，S1F23 的事件 VID 名录也会重复。
            reportid.VIDID = Dedup(pVIDID);
            Save();
        }
    }
}