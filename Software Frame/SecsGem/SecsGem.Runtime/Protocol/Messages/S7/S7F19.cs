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
    /// S7F19：查询设备内存目录的 PPID 列表，不含参数、不切换配方。
    /// 方向：S7F19 H→E / S7F20 E→H。
    /// 请求无正文；回复 L[n] A PPID，空目录为 L[0]。不会自动读取 MachineApp 配方库。
    /// </summary>
    [SecsOrder("S7F19")]
    class S7F19 : SecsOrderBase
    {
        /// <summary>读取 PPID 列表并回复 S7F20；请求带正文或组包失败时返回 null，走 S9F7。</summary>
        public override Item GetReplyItem(SecsMessage pSecsMessage)
        {
            try
            {
                // S7F19 是 header-only，L[0] 也不是合法正文。
                if (pSecsMessage.SecsItem != null) return null;
                // 直接从 DeviceManager 获取当前过程程序目录并构建 S7F20 回复
                
                var ppList = DeviceManager?.GetProcessProgramDirectory() ?? new List<ProcessProgramInfo>();
                
                var items = new List<Item>();
                foreach (var pp in ppList)
                {
                    if (!string.IsNullOrEmpty(pp.PPID))
                    {
                        items.Add(Item.A(pp.PPID));
                    }
                }

                // S7F20: L, n (过程程序列表)
                return Item.L(items.ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"S7F19 生成 S7F20 失败: {ex.Message}");
                // 不把内部错误伪装成“目录为空”；由传输层走 S9F7。
                return null;
            }
        }
    }

    /// <summary>
    /// 内存目录中的过程程序信息；不是机台当前选中配方或完整业务配方模型。
    /// </summary>
    public class ProcessProgramInfo
    {
        /// <summary>
        /// 配方标识符，当前实现使用 ASCII 字符串；数字形式的编号也保留前导零。
        /// </summary>
        public string PPID { get; set; } = string.Empty;

        /// <summary>
        /// 兼容旧调用保留的可选文本字段，不进入 S7F20，也不是 S7F3 的二进制 PPBODY 存储。
        /// </summary>
        public string PPBODY { get; set; } = string.Empty;
    }
}