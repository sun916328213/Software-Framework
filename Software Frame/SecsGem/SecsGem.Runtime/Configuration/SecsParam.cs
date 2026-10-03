// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：SecsParam。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.IO.Files、Extension.IO.Csv、Extension.IO.Retention、Extension.Core.Conversion。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Conversion;
using System.IO;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    /// SecsParam 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    public class SecsParam
    {
        internal  SecsGemOptions _SecsParam ;

        /// <summary>
        /// 文件路径
        /// </summary>
        internal string FilePath = "Config//SecsJson";

        /// <summary>
        /// 文件名
        /// </summary>
        internal string FileName= "SecsParam.json";

        /// <summary>
        /// 初始化 SecsParam 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Load；具体条件和先后次序见方法体。
        /// </summary>
        public SecsParam()
        {
            _SecsParam= _SecsParam ??  Load();
        }

        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 Read、FromJson；具体条件和先后次序见方法体。
        /// </summary>
        public SecsGemOptions Load()
        {
            var data = FileEx.Read(FilePath, FileName);
            return data.FromJson<SecsGemOptions>();
        }

        /// <summary>
        /// 执行保存操作；对应入口为 Save。
        /// 正文中的关键协作调用包括 ToJson、Write；具体条件和先后次序见方法体。
        /// </summary>
        public void Save()
        {
            string data = _SecsParam.ToJson();
            data.Write(FilePath, FileName);
        }
    }
}
