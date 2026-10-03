// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：ECID。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Prism.Ioc。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Prism.Ioc;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// ECID 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：VIDBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class ECID:VIDBase
    {
        /// <summary>
        /// 初始化 ECID 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public ECID( )  
        {
        }

        public override string FileName { get  ; set  ; } ="ECID.csv";
    }
}
