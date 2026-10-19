// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S5F3。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Prism.Ioc、SecsGem.Runtime.Variable。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Linq;

namespace SecsGem.Runtime.Order.S5
{
    /// <summary>
    /// 启用报警信息 H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例（当前实现以数值 1 为启用，不把 0x80 解释为 1）：
    /// <code><![CDATA[
    /// 'S5F3'W                         // Host→设备，启用/禁用报警
    /// <L [2]                          // 启用标记、报警 ID 列表
    ///     <B [1] 0x01>                // ALED：当前 1=启用，其余=禁用
    ///     <L [1]                      // 指定一条报警；空列表为全部
    ///         <A [4] '2001'>          // ALID：已配置报警 ID
    ///     >
    /// >
    /// .
    /// 'S5F4'                          // 设备→Host，报警开关应答
    /// <B [1] 0x00>                    // ACKC5：0=接受
    /// .
    /// ]]></code>
    /// 会保存报警配置；支持列表及逗号分隔 ID 的兼容输入。这里展示现有行为，不将其宣称为所有 Host 的 ALED 规范。
    /// </remarks>
    [SecsOrder("S5F3")]
    class S5F3 : SecsOrderBase
    {
     

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、Select、GetDatas、Contains、Split；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage   )
        {
            Item SecsItem;
            try
            {
                //解析数据
                var data = SecsItemToList(pSecsMessage.SecsItem);
                if (data == null || data.Count == 0)
                    throw new Exception("S5F3 body 为空");

                //格式
                bool enable = data[0] == "1";

                Alarm alarm = VIDManager. Alarm;

                // ALID 形状有两种：标准列表平铺（Flatten 后是 data[1..] 逐个），
                // 或单个逗号串（部分 Host 的写法）；空 = 全量开关。
                var ids = data.Skip(1)
                    .SelectMany(s => (s ?? string.Empty).Split(','))
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToArray();
                if (ids.Length == 0)
                {
                    foreach (var s in alarm.GetDatas()) s.Enable = enable;
                }
                else
                {
                    foreach (var id in ids)
                    {
                        alarm.SetEnable(id, enable);
                    }
                }
                alarm.Save();
                SecsItem = Item.B(0);//succsee
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
           
        }

     
    }
}
