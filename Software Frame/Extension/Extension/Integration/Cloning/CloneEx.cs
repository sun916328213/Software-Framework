// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：CloneEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：FastDeepCloner。导入不代表所有类型都会在运行时加载。
using FastDeepCloner;

namespace Extension.Integration.Cloning
{
    /// <summary>
    /// CloneEx 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// </summary>
    public static class CloneEx
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 DeepClone。
        /// 正文中的关键协作调用包括 Clone；具体条件和先后次序见方法体。
        /// </summary>
        public static T DeepClone<T>(this T obj)
        {
            return (T)((object)obj).Clone();
        }
    }
}
