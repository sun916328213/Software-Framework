// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S7F25、S7F25SendData、ProcessProgramData。成员说明可结合下方具体实现阅读。
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
    /// S7F25 - Formatted Process Program Request (Host → Equipment)
    /// 请求设备发送指定的格式化过程程序
    /// SEMI E30 GEM Standard
    /// 结构: L, 1
    ///   1. <A> PPID (Process Program ID)
    /// 回复 S7F26: L, 2
    ///   1. <A> PPID
    ///   2. <L, n> Formatted Process Program Body
    /// </summary>
    [SecsOrder("S7F25")]
    class S7F25 : SecsOrderBase
    {
        /// <summary>
        /// S7F25 发送参数
        /// </summary>
        public class S7F25SendData
        {
            /// <summary>
            /// 要请求的过程程序 ID
            /// </summary>
            public string PPID { get; set; } = string.Empty;
        }

        /// <summary>
        /// 发送 S7F25 - 请求格式化过程程序
        /// pData[0]: S7F25SendData 或 PPID (string)
        /// </summary>
        public override Item GetSendItem(params object[] pData)
        {
            try
            {
                string ppid = string.Empty;

                if (pData != null && pData.Length > 0)
                {
                    if (pData[0] is S7F25SendData sendData)
                    {
                        ppid = sendData.PPID;
                    }
                    else if (pData[0] is string strPpid)
                    {
                        ppid = strPpid;
                    }
                }

                if (string.IsNullOrEmpty(ppid))
                {
                    System.Diagnostics.Debug.WriteLine("S7F25: PPID 不能为空");
                    return null;
                }

                // S7F25 消息结构: L, 1
                //   1. <A> PPID
                Item secsMessage = Item.L(
                    Item.A(ppid)
                );

                return secsMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F25 构建失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析 S7F26 回复 - Formatted Process Program Data
        /// 结构: L, 2
        ///   1. <A> PPID
        ///   2. <L, n> Formatted Process Program Body
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                var secsItem = pSecsMessage.SecsItem;
                if (secsItem == null || secsItem.Format != SecsFormat.List || secsItem.Items == null || secsItem.Items.Length < 2)
                {
                    System.Diagnostics.Debug.WriteLine("S7F26 回复格式错误");
                    return Item.B(0);
                }

                // 解析 PPID
                string ppid = secsItem.Items[0].GetString();

                // 解析 PPBODY
                var bodyItem = secsItem.Items[1];
                List<object> ppbody = new List<object>();

                if (bodyItem.Format == SecsFormat.List && bodyItem.Items != null)
                {
                    foreach (var item in bodyItem.Items)
                    {
                        ppbody.Add(ParseSecsItem(item));
                    }
                }
                else
                {
                    // 单个数据项
                    ppbody.Add(ParseSecsItem(bodyItem));
                }

                // 创建过程程序数据对象
                var programData = new ProcessProgramData
                {
                    PPID = ppid,
                    PPBODY = ppbody,
                    ReceivedTime = DateTime.Now
                };

                // 更新设备的过程程序数据
                if (DeviceManager != null)
                {
                    DeviceManager.UpdateProcessProgramData(programData);
                }

                System.Diagnostics.Debug.WriteLine($"S7F25 收到 S7F26 回复: PPID={ppid}, Body项数={ppbody.Count}");

                return secsItem;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F25 解析 S7F26 失败: {ex.Message}");
                return Item.B(0);
            }
        }

        /// <summary>
        /// 解析 SECS Item 为 .NET 对象
        /// </summary>
        private object ParseSecsItem(Item item)
        {
            try
            {
                if (item == null) return null;

                return item.Format switch
                {
                    SecsFormat.ASCII => item.GetString(),
                    SecsFormat.Binary => item.GetMemory<byte>().ToArray(),
                    SecsFormat.Boolean => item.GetMemory<bool>().Span[0],
                    SecsFormat.I1 => item.GetMemory<sbyte>().Span[0],
                    SecsFormat.I2 => item.GetMemory<short>().Span[0],
                    SecsFormat.I4 => item.GetMemory<int>().Span[0],
                    SecsFormat.I8 => item.GetMemory<long>().Span[0],
                    SecsFormat.U1 => item.GetMemory<byte>().Span[0],
                    SecsFormat.U2 => item.GetMemory<ushort>().Span[0],
                    SecsFormat.U4 => item.GetMemory<uint>().Span[0],
                    SecsFormat.U8 => item.GetMemory<ulong>().Span[0],
                    SecsFormat.F4 => BitConverter.ToSingle(item.GetMemory<byte>().Span),
                    SecsFormat.F8 => BitConverter.ToDouble(item.GetMemory<byte>().Span),
                    SecsFormat.List => ParseListItem(item),
                    _ => item.GetString()
                };
            }
            catch
            {
                return item?.GetString() ?? string.Empty;
            }
        }

        /// <summary>
        /// 解析列表类型 Item
        /// </summary>
        private List<object> ParseListItem(Item item)
        {
            var list = new List<object>();
            if (item.Items != null)
            {
                foreach (var subItem in item.Items)
                {
                    list.Add(ParseSecsItem(subItem));
                }
            }
            return list;
        }
    }

    /// <summary>
    /// 过程程序数据
    /// </summary>
    public class ProcessProgramData
    {
        /// <summary>
        /// 过程程序 ID
        /// </summary>
        public string PPID { get; set; } = string.Empty;

        /// <summary>
        /// 过程程序本体数据
        /// </summary>
        public List<object> PPBODY { get; set; } = new List<object>();

        /// <summary>
        /// 接收时间
        /// </summary>
        public DateTime ReceivedTime { get; set; }
    }
}