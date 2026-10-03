// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S7F23、S7F23SendData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Core.Order、SecsGem.Runtime、SecsGem.Runtime.Order、Secs4Net、SecsGem.Runtime.Manager。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Core.Order;
using SecsGem.Runtime;
using SecsGem.Runtime.Order;
using Secs4Net;
using SecsGem.Runtime.Manager;
using System;
using System.Collections.Generic;

namespace SecsGem.Runtime.Order.S7
{
    /// <summary>
    /// S7F23 - Formatted Process Program Send (Host → Equipment)
    /// 发送格式化过程程序到设备
    /// SEMI E30 GEM Standard
    /// 结构: L, 2
    ///   1. <A> PPID (Process Program ID)
    ///   2. <L, n> Formatted Process Program Body
    /// </summary>
    [SecsOrder("S7F23")]
    class S7F23 : SecsOrderBase
    {
        /// <summary>
        /// S7F23 发送参数
        /// </summary>
        public class S7F23SendData
        {
            /// <summary>
            /// 过程程序 ID
            /// </summary>
            public string PPID { get; set; } = string.Empty;

            /// <summary>
            /// 格式化过程程序本体数据
            /// 可以是字符串列表或结构化数据列表
            /// </summary>
            public List<object> PPBODY { get; set; } = new List<object>();
        }

        /// <summary>
        /// 发送 S7F23 - 发送格式化过程程序
        /// pData[0]: S7F23SendData 或 PPID (string)
        /// pData[1]: PPBODY (List<object>, 可选)
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            try
            {
                string ppid = string.Empty;
                List<object> ppbody = new List<object>();

                if (pData != null && pData.Length > 0)
                {
                    if (pData[0] is S7F23SendData sendData)
                    {
                        ppid = sendData.PPID;
                        ppbody = sendData.PPBODY;
                    }
                    else if (pData[0] is string strPpid)
                    {
                        ppid = strPpid;
                        if (pData.Length > 1 && pData[1] is List<object> body)
                        {
                            ppbody = body;
                        }
                    }
                }

                if (string.IsNullOrEmpty(ppid))
                {
                    System.Diagnostics.Debug.WriteLine("S7F23: PPID 不能为空");
                    return null;
                }

                // 构建 PPBODY 列表
                List<Item> bodyItems = new List<Item>();
                foreach (var item in ppbody)
                {
                    if (item != null)
                    {
                        // 根据数据类型转换为 SECS Item
                        Item secsItem = ConvertToSecsItem(item);
                        if (secsItem != null)
                            bodyItems.Add(secsItem);
                    }
                }

                // S7F23 消息结构: L, 2
                //   1. <A> PPID
                //   2. <L, n> PPBODY
                Item secsMessage = Item.L(
                    Item.A(ppid),
                    Item.L(bodyItems.ToArray())
                );

                return secsMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F23 构建失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 S7F24 回复 - Formatted Process Program Acknowledge
        /// 结构: <B> ACKC7
        ///   0 = 接受
        ///   1 = 拒绝: PPID 不存在
        ///   2 = 拒绝: 格式错误
        ///   3 = 拒绝: 设备状态不允许
        ///   4 = 拒绝: 存储空间不足
        ///   >4 = 其他错误
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var secsItem = pSecsMessage.SecsItem;
                if (secsItem == null || secsItem.Format != SecsFormat.Binary)
                    return Item.B(2); // 格式错误

                byte ackc7 = 0;
                var memory = secsItem.GetMemory<byte>();
                if (!memory.IsEmpty)
                {
                    ackc7 = memory.Span[0];
                }

                // 根据 ACKC7 码处理
                string message = ackc7 switch
                {
                    0 => "过程程序接收成功",
                    1 => "拒绝: PPID 不存在",
                    2 => "拒绝: 格式错误",
                    3 => "拒绝: 设备状态不允许",
                    4 => "拒绝: 存储空间不足",
                    _ => $"拒绝: 未知错误码 {ackc7}"
                };

                System.Diagnostics.Debug.WriteLine($"S7F23 收到 S7F24 回复: ACKC7={ackc7}, {message}");

                // 可以在这里触发事件或更新状态
                if (DeviceManager != null)
                {
                    DeviceManager.OnProcessProgramSendResult(ackc7 == 0, message);
                }

                return secsItem; // 返回原始 ACKC7
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F23 解析 S7F24 失败: {ex.Message}");
                return Item.B(255);
            }
        }

        /// <summary>
        /// 将对象转换为 SECS Item
        /// </summary>
        private Item ConvertToSecsItem(object obj)
        {
            try
            {
                if (obj == null) return Item.A(string.Empty);

                Type type = obj.GetType();
                
                if (type == typeof(string))
                    return Item.A((string)obj);
                else if (type == typeof(byte) || type == typeof(byte[]))
                    return Item.B(obj is byte b ? new Memory<byte>(new[] { b }) : (byte[])obj);
                else if (type == typeof(bool))
                    return Item.Boolean(Convert.ToBoolean(obj));
                else if (type == typeof(sbyte))
                    return Item.I1(Convert.ToSByte(obj));
                else if (type == typeof(short))
                    return Item.I2(Convert.ToInt16(obj));
                else if (type == typeof(int))
                    return Item.I4(Convert.ToInt32(obj));
                else if (type == typeof(long))
                    return Item.I8(Convert.ToInt64(obj));
                else if (type == typeof(ushort))
                    return Item.U2(Convert.ToUInt16(obj));
                else if (type == typeof(uint))
                    return Item.U4(Convert.ToUInt32(obj));
                else if (type == typeof(ulong))
                    return Item.U8(Convert.ToUInt64(obj));
                else if (type == typeof(float))
                    return Item.F4(Convert.ToSingle(obj));
                else if (type == typeof(double))
                    return Item.F8(Convert.ToDouble(obj));
                else if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
                {
                    // 处理列表/数组
                    var items = new List<Item>();
                    foreach (var item in enumerable)
                    {
                        var secsItem = ConvertToSecsItem(item);
                        if (secsItem != null)
                            items.Add(secsItem);
                    }
                    return Item.L(items.ToArray());
                }
                else
                {
                    // 默认按字符串处理
                    return Item.A(obj.ToString());
                }
            }
            catch
            {
                return Item.A(obj?.ToString() ?? string.Empty);
            }
        }
    }
}