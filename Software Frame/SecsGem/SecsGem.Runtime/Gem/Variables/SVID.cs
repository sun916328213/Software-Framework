// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SVID。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using Secs4Net;
using SecsGem.Core;
using SecsGem.Runtime;
using Prism.Ioc;
using System.Linq;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// SVID 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：VIDBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SVID : VIDBase
    {
        /// <summary>
        /// 初始化 SVID 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SVID( )  
        {
        }

        public  override string FileName { get  ; set  ; }= "SVID.csv";

        public override string Category { get; } = "SVID";

        public override string FilePath { get; set; } = "Config//SecsJson//SVID";

        /// <summary>
        /// 缺 CSV 时的打底项：GEM 标准时间 + 控制状态。
        /// DeviceMode/DeviceStatus 不给初始值——值由 <c>StatusManager</c> 运行时写入，
        /// 这里给死值反而会掩盖「监控没启动」这个问题。
        /// </summary>
        protected override void LoadDefaults()
        {
            foreach (var v in ProtectedItems()) Datas.Add(v);
        }

        /// <summary>
        /// 受保护的常用项：界面上删掉后，下次 <c>Load</c> 按 ID 补回来。
        /// </summary>
        protected override void EnsureDefaults()
        {
            foreach (var v in ProtectedItems())
            {
                if (Datas.All(x => x.ID != v.ID))
                {
                    Datas.Add(v);
                    System.Diagnostics.Debug.WriteLine($"SVID 缺常用项 {v.ID}_{v.Name}，已补回");
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<VIDInfo> ProtectedItems()
        {
            yield return new VIDInfo { ID = "101", Name = "Clock", DataTypeName = nameof(SecsFormat.ASCII) };
            yield return new VIDInfo { ID = "102", Name = "DeviceMode", DataTypeName = nameof(SecsFormat.U2) };
            yield return new VIDInfo { ID = "103", Name = "DeviceStatus", DataTypeName = nameof(SecsFormat.U2) };
        }
    }
}
