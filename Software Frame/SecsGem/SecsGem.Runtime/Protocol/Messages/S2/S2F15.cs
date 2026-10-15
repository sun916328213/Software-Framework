// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F15。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、SecsGem.Runtime.Variable、Secs4Net、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S2
{
    /// <summary>
    /// 新设备常量发送 New Equipment Constant Send H->E（E5 S2F15）。
    /// <para>
    /// 请求体 <c>L[n] &lt;L[2] &lt;ECID&gt; &lt;ECV&gt;&gt;</c>，平铺后是 ECID/ECV 成对出现；
    /// 回 S2F16 <c>B[1] &lt;EAC&gt;</c>：0=ACK、1=INVALID_CONSTANT、2=BUSY、3=OUT_OF_RANGE。
    /// 标准要求"任一值非法则整条都不设置"，所以这里先全量校验再统一写入。
    /// </para>
    /// </summary>
    [SecsOrder("S2F15")]
    class S2F15 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、FindECID、B；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem) ?? new List<string>();
                if (data.Count == 0 || data.Count % 2 != 0)
                    throw new Exception("S2F15 请求体不是 ECID/ECV 成对列表");

                // 先整条校验：任一 ECID 不认识就回 EAC=1，一个值都不落。
                // 原来是边校验边写，第一个认得、第二个不认得时第一个已经被改掉，
                // 现场表现是"下发的常量值一半生效"，Host 侧却收到拒绝码。
                var pairs = new List<KeyValuePair<VIDInfo, string>>(data.Count / 2);
                for (int index = 0; index < data.Count; index += 2)
                {
                    var ecid = FindECID(data[index]);
                    if (ecid == null)
                    {
                        SecsGemRuntime?.RaiseErrorPublic($"S2F15 拒绝：ECID {data[index]} 不存在（EAC=1 INVALID_CONSTANT）");
                        return Item.B(1);
                    }

                    pairs.Add(new KeyValuePair<VIDInfo, string>(ecid, data[index + 1]));
                }

                foreach (var pair in pairs)
                    pair.Key.Value = pair.Value;

                return Item.B(0); //EAC=0 ACK
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
