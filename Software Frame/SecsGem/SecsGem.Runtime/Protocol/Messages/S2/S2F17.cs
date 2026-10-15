// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S2F17。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core.Order、SecsGem.Runtime.Order、Secs4Net、System。导入不代表所有类型都会在运行时加载。
using SecsGem.Core.Order;
using SecsGem.Runtime.Order;
using Secs4Net;
using System;

namespace SecsGem.Runtime.Order.S2
{
    /// <summary>
    /// 日期时间请求 Date and Time Request H->E（E5 S2F17，无请求体），回 S2F18 <c>TIME</c>。
    /// <para>
    /// TIME 的格式由 GEM 的 TimeFormat ECV 决定；本设备没有定义该 ECV，
    /// 统一用 24 小时制 16 位 <c>yyyyMMddHHmmssff</c>（与 S6F1 的 STIME 同格式），
    /// 这样 Host 侧对这两条时间只需一套解析规则。
    /// </para>
    /// </summary>
    [SecsOrder("S2F17")]
    class S2F17 : SecsOrderBase
    {
        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 ToString、A；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                return Item.A(DateTime.Now.ToString("yyyyMMddHHmmssff"));
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
