// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S5F5。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Extension.Core.Buffers、Extension.IO.Compression。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Prism.Ioc;
using SecsGem.Runtime.Variable;
using Secs4Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SecsGem.Runtime.Order.S5
{
    /// <summary>
    /// 报警信息List H->E
    /// </summary>
    /// <remarks>
    /// 指令格式示例：查询已有报警 2001，假设当前 Code=1。
    /// <code><![CDATA[
    /// 'S5F5'W                         // Host→设备，请求报警列表
    /// <L [1]                          // 单个查询项；L[0] 查询全部
    ///     <A [4] '2001'>              // ALID：当前按首项读取；批量可用逗号串
    /// >
    /// .
    /// 'S5F6'                          // 设备→Host，报警列表应答
    /// <L [1]                          // 本例匹配一条报警
    ///     <L [3]                      // ALCD、ALID、ALTX
    ///         <B [1] 0x80>            // ALCD：本实现将 Code 左移七位
    ///         <A [4] '2001'>          // ALID：ASCII 报警 ID
    ///         <A [9] 'Door open'>     // ALTX：报警描述
    ///     >
    /// >
    /// .
    /// ]]></code>
    /// 当前多个查询 ID 从首个字符串按逗号拆分，不会逐项处理任意 ALID 列表；无匹配返回 null，走 S9F7。
    /// </remarks>
    [SecsOrder("S5F5")]
    class S5F5 : SecsOrderBase
    {
     

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetReplyItem。
        /// 正文中的关键协作调用包括 SecsItemToList、GetDatas、Add、SetValue、ToDictionary；具体条件和先后次序见方法体。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage )
        {
            try
            {
                var data = SecsItemToList(pSecsMessage.SecsItem);
                Alarm alarm = VIDManager.Alarm;
 
               var  _alarmcopy = alarm.GetDatas();

                List<Item> Items = new List<Item>();

                if (data == null || data.Count == 0)
                {
                    foreach (AlarmInfo curalarm in _alarmcopy)
                    {
                        Items.Add(S5F1.SetValue(curalarm));
                    }
                }
                else
                {
                    var _alarmcopybyid = _alarmcopy.ToDictionary(x => x.ID, x => x);
                    string ids = data[0];
                    string[] idarray = ids.Split(',');
                    foreach (var id in idarray)
                    {
                        if (_alarmcopybyid.TryGetValue(id, out var curalarm))
                            Items.Add(S5F1.SetValue(curalarm));
                    }
                }

                if (Items.Count == 0) return null;
                Item SecsItem = Item.L(Items.ToArray()); ;
                return SecsItem;
            }
            catch (Exception ex)
            {
                return Fail(ex);  //  解析失败 返回null TryReplyAsync()中replyMessage=null  触发S9F7 
            }
        }
    }
}
