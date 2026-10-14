// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ECID。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using Secs4Net;
using SecsGem.Core;
using SecsGem.Runtime;
using Prism.Ioc;
using System.Linq;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// ECID 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：VIDBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ECID:VIDBase
    {
        /// <summary>
        /// 初始化 ECID 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ECID( )  
        {
        }

        public override string FileName { get  ; set  ; } ="ECID.csv";

        public override string Category { get; } = "ECID";

        public override string FilePath { get; set; } = "Config//SecsJson//ECID";

        /// <summary>
        /// 缺 CSV 时的打底项：MDLN / SOFTREV 这对 E5 §7 要求的设备常量。
        /// 值留空由 <c>DeviceManager</c> 运行时填，这里只保证 S2F33/S2F35 不会因找不到变量而抛。
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
                    System.Diagnostics.Debug.WriteLine($"ECID 缺常用项 {v.ID}_{v.Name}，已补回");
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<VIDInfo> ProtectedItems()
        {
            yield return new VIDInfo { ID = "104", Name = "DeviceName", DataTypeName = nameof(SecsFormat.ASCII) };
            yield return new VIDInfo { ID = "105", Name = "DeviceSoftWare", DataTypeName = nameof(SecsFormat.ASCII) };
        }
    }
}
