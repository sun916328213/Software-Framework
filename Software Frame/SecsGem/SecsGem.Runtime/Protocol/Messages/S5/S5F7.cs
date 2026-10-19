// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S5F7。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S5
{
    /// <summary>
    /// Enable报警信息集合 H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：查询全部已启用报警，假设只启用 2001，且 Code=1。
    /// <code><![CDATA[
    /// 'S5F7'W                         // Host→设备，查询已启用报警
    /// .                              // 无正文
    /// 'S5F8'                          // 设备→Host，已启用报警列表
    /// <L [1]                          // 本例一条；不等于当前发生的报警列表
    ///     <L [3]                      // ALCD、ALID、ALTX
    ///         <B [1] 0x80>            // ALCD：本实现将 Code 左移七位
    ///         <A [4] '2001'>          // ALID：ASCII 报警 ID
    ///         <A [9] 'Door open'>     // ALTX：报警描述
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// 过滤依据 Enable=true，不是 Code=1；当前无启用报警返回 null，走 S9F7，不伪装成成功空列表。
    /// </remarks>
    [SecsOrder("S5F7")]
    class S5F7 : SecsOrderBase
    {
     

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 GetDatas、Add、SetValue、L、ToArray；具体条件和先后次序见方法体。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage  )
        {
            Alarm alarm = VIDManager.Alarm;

            List<AlarmInfo> _alarmcopy = alarm.GetDatas();
           
            List<Item> Items = new List<Item>();
            if (_alarmcopy != null) 
            {

                foreach (AlarmInfo curalarm in _alarmcopy)
                {
                    if (curalarm.Enable)
                    {
                        Items.Add(S5F1.SetValue(curalarm));
                    }
                }

                if (Items.Count == 0) return null;
                Item SecsItem = Item.L(Items.ToArray());
                return SecsItem;
            }
            else
                return null;
        }
    }
}
