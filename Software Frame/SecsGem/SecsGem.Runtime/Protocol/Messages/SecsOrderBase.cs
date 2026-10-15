// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsOrderBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、Prism.Ioc、SecsGem.Runtime.Protocol.Building、SecsGem.Runtime.Protocol.Parsing、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using Prism.Ioc;
using SecsGem.Runtime.Protocol.Building;
using SecsGem.Runtime.Protocol.Parsing;
using SecsGem.Runtime.Manager;
using SecsGem.Runtime.Variable;
using Secs4Net;
using SecsGem.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SecsGem.Runtime.Order
{
    /// <summary>
    /// SecsOrderBase 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public class SecsOrderBase
    {
        IContainerProvider ContainerProvider;

        internal SecsGemRuntime SecsGemRuntime => ContainerProvider?.Resolve<SecsGemRuntime>();
        internal VIDManager VIDManager => ContainerProvider?.Resolve<VIDManager>();
        internal DeviceManager DeviceManager => ContainerProvider?.Resolve<DeviceManager>();
        internal StatusManager StatusManager => ContainerProvider?.Resolve<StatusManager>();
        internal TraceManager TraceManager => ContainerProvider?.Resolve<TraceManager>();

        /// <summary>
        /// 初始化 SecsOrderBase 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SecsOrderBase() { }
        
        // 通过属性注入设置容器提供者
        public IContainerProvider ContainerProviderSetter
        {
            set { ContainerProvider = value; }
        }

        /// <summary>
        /// 接收数据 Host->Device 
        /// </summary>
        /// <param name="pSecsMessage"></param>
        /// <returns></returns>
        public virtual  Item  GetReplyItem(SecsMessage pSecsMessage)
        {
            // 使用Item.B(0)的静态实例减少内存分配
            return Item.B(0);
        }

        /// <summary>
        /// 异步回包入口，默认包装同步 GetReplyItem。
        /// 需要异步处理（如回调业务方）的指令重写此方法。
        /// 返回 null 时由底层自动触发 S9F7 兜底。
        /// </summary>
        public virtual Task<Item> GetReplyItemAsync(SecsMessage pSecsMessage)
        {
            return Task.FromResult(GetReplyItem(pSecsMessage));
        }
 
        //发送------------------------------------------------------------------------------------------------
 
        /// <summary>
        /// 发送数据 Device->Host
        /// </summary>
        /// <returns></returns>
        public virtual Item GetSendItem(params object[] pData)
        {
            // 使用Item.B(0)的静态实例减少内存分配
            return Item.B(0);
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="pData"></param>
        /// <param name="DataType"></param>
        /// <returns></returns>
        public virtual Item AddData(object pData, SecsFormat DataType)
        {
            return SecsItemBuilder.Build(pData, DataType);
        }

        /// <summary>  
        /// 解析数据  
        /// </summary>  
        /// <param name="Data"></param>  
        public virtual List<string> SecsItemToList(Item Data)
        {
            return SecsItemParser.Flatten(Data);
        }

        /// <summary>  
        /// 获取数据 uint  
        /// </summary>  
        /// <typeparam name="T">数据类型</typeparam>  
        /// <param name="pItem">数据 格式:Item</param>  
        /// <returns></returns>  
        public virtual string GetValue<T>(Item pItem) where T : unmanaged, IEquatable<T>
        {
            return SecsItemParser.ReadFirstValue<T>(pItem);
        }

        /// <summary>
        /// 组包失败统一出口：把原因报进 SEMILog（Host 那边只会看到 S9F7，看不到为什么），返回 null。
        /// <para>
        /// 原实现是 <c>catch { string message = ex.Message + ex.StackTrace; return null; }</c> ——
        /// message 拼完就丢，现场只剩一条没头没尾的 S9F7，无法定位是哪条引用不成立。
        /// </para>
        /// </summary>
        protected Item Fail(Exception ex)
        {
            SecsGemRuntime?.RaiseErrorPublic($"组包失败，本条回 S9F7: {ex.Message}");
            return null;
        }

        /// <summary>
        /// 按 ID 取设备常量（ECID 表里的项）。S2F13/S2F15/S2F29 都是 ECID 专用，
        /// 不能拿 <c>GetVIDByID</c> 直接当 ECID 用——那个会命中 SVID/DVID，
        /// 结果是把采样变量当常量回给 Host。
        /// </summary>
        protected VIDInfo FindECID(string pId)
        {
            var vid = VIDManager?.GetVIDByID(pId);
            return vid != null && vid.Category == "ECID" ? vid : null;
        }

        /// <summary>一条链接请求里的"一组"：LinkId 是被改的对象，Members 是挂上去的 ID 列表。</summary>
        protected readonly record struct LinkGroup(string LinkId, List<string> Members);

        /// <summary>
        /// 按"组"解析链接类请求（S2F33 定义报表、S2F35 链接事件）。
        /// <para>
        /// 标准形状：<c>L[3] &lt;DATAID&gt; &lt;RPTID/CEID&gt; &lt;L[成员]&gt;</c>（成员也可以直接平铺在后面）。
        /// 产线 Host 实测还多一种：<c>L[2] &lt;DATAID&gt; &lt;L[n] &lt;L[2] &lt;RPTID/CEID&gt; &lt;L[成员]&gt;&gt;…&gt;&gt;</c>
        /// —— DATAID 只有一份在最外层，每组只有 <c>RPTID + 成员</c> 两项，外面套两层列表。
        /// </para>
        /// <para>
        /// 所以这里递归下钻找组，套几层、每组几项、成员带不带内层列表，都不猜：
        /// 只要"头两项是标量"就是一组，成员从第三项起（两项形状从第二项起）。
        /// 外层那个 <c>L[2] &lt;DATAID&gt; &lt;La…&gt;</c> 第二项是列表，天然不满足判据，会继续往下钻。
        /// </para>
        /// <para>
        /// 原实现按 data[0]/data[1]/Skip(2) 平铺取，键和值的对应关系全丢：第二组的数据被当成
        /// 第一组的数据（RPTID/CEID 被当 VID，或另一个 CEID 被当报表挂上去），
        /// 现场表现是"第二条链接不生效、变量全挂在第一个报表下面"。
        /// </para>
        /// </summary>
        protected List<LinkGroup> SplitLinkGroups(Item body)
        {
            var groups = new List<LinkGroup>();
            CollectLinkGroups(body, groups, 0);
            return groups;
        }

        /// <summary>递归找组；depth 挡异常报文无限套娃，正常报文两层就够。</summary>
        private static void CollectLinkGroups(Item node, List<LinkGroup> groups, int depth)
        {
            // 只往下走列表；标量（成员列表里的那些 ID）不是组，停在原地。
            if (node?.Format != SecsFormat.List || depth > 4) return;

            var items = node.Items;
            if (items.Length >= 2 && items[0].Format != SecsFormat.List)
            {
                if (items[1].Format != SecsFormat.List && items.Length >= 3)
                {
                    // <DATAID> <RPTID/CEID> <成员…>，成员平铺或带内层 L 都行
                    groups.Add(new LinkGroup(
                        First(items[1]),
                        SecsItemParser.Flatten(Item.L(items.Skip(2).ToArray())) ?? new List<string>()));
                    return;
                }

                if (items[1].Format == SecsFormat.List && items.Length == 2
                    && items[1].Items.All(x => x.Format != SecsFormat.List))
                {
                    // <RPTID/CEID> <L[成员]>，DATAID 在外层
                    groups.Add(new LinkGroup(
                        First(items[0]),
                        SecsItemParser.Flatten(items[1]) ?? new List<string>()));
                    return;
                }
            }

            foreach (var sub in items)
                CollectLinkGroups(sub, groups, depth + 1);
        }

        private static string First(Item item) => SecsItemParser.Flatten(item)?.FirstOrDefault() ?? string.Empty;

    }

}
