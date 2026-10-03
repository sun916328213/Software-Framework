// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：S7F19、ProcessProgramInfo。成员说明可结合下方具体实现阅读。
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
    /// S7F19 - Current EPPD Request (Host → Equipment)
    /// 请求当前有效的设备过程程序目录 (Equipment Process Program Directory)
    /// SEMI E30 GEM Standard
    /// 设备端收到 S7F19 请求，回复 S7F20
    /// </summary>
    [SecsOrder("S7F19")]
    class S7F19 : SecsOrderBase
    {
        /// <summary>
        /// 收到 S7F19 请求，生成 S7F20 回复 - Current EPPD Data
        /// S7F19 请求体为空，直接返回当前过程程序目录
        /// S7F20 结构: L, n
        ///   1. <L, 2> Process Program List
        ///      1. <A> PPID (Process Program ID)
        ///      2. <A> PPBODY (可选, 格式化过程程序本体)
        /// </summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                // S7F19 请求体为空，忽略 pSecsMessage.SecsItem
                // 直接从 DeviceManager 获取当前过程程序目录并构建 S7F20 回复
                
                var ppList = DeviceManager?.GetProcessProgramDirectory() ?? new List<ProcessProgramInfo>();
                
                var items = new List<Item>();
                foreach (var pp in ppList)
                {
                    if (!string.IsNullOrEmpty(pp.PPID))
                    {
                        // 每个过程程序: L, 2  (PPID, PPBODY)
                        var ppItem = Item.L(
                            Item.A(pp.PPID),
                            Item.A(pp.PPBODY ?? string.Empty)
                        );
                        items.Add(ppItem);
                    }
                }

                // S7F20: L, n (过程程序列表)
                return Item.L(items.ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F19 生成 S7F20 失败: {ex.Message}");
                // 出错返回空列表
                return Item.L();
            }
        }
    }

    /// <summary>
    /// 过程程序信息
    /// </summary>
    public class ProcessProgramInfo
    {
        /// <summary>
        /// 过程程序 ID
        /// </summary>
        public string PPID { get; set; } = string.Empty;

        /// <summary>
        /// 过程程序本体 (可选)
        /// </summary>
        public string PPBODY { get; set; } = string.Empty;
    }
}