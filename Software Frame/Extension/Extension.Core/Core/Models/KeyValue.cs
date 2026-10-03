// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：KeyValue。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Core.Models
{
    /// <summary>
    /// 键值对
    /// </summary>
    public class KeyValue
    {
        /// <summary>
        /// 键
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// 值
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 初始化 KeyValue 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public KeyValue() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public KeyValue(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
