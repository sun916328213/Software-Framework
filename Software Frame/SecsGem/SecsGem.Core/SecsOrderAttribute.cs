// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsOrderAttribute。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、System。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using System;

namespace SecsGem.Core.Order
{
    /// <summary>
    /// Secs指令注册特性，标注指令处理器对应的 SxFy 名称。
    /// 工厂启动时优先读取该特性，未标注时按类名规则兜底并告警。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SecsOrderAttribute : Attribute
    {
        /// <summary>
        /// 指令名称，如 "S6F11"
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 初始化 SecsOrderAttribute 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SecsOrderAttribute(string name)
        {
            Name = name;
        }
    }
}
