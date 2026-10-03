// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：IData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Collections、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Collections;
using System.Collections.Generic;

namespace SecsGem.Runtime.Variable
{

    /// <summary>
    /// IData 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// </summary>
    interface IData<T>
    {
 
        /// <summary>
        /// 文件路径
        /// </summary>
        string FilePath { get; set; }

        /// <summary>
        /// 文件名称
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        ///  数据集合
        /// </summary>
        ConcurrentList<T> Datas { get; set; }

        /// <summary>
        /// 加载数据
        /// </summary>
        void Load();

        /// <summary>
        /// 保存数据到文件
        /// </summary>
        void Save();

        /// <summary>
        /// 获取数据集合
        /// </summary>
        /// <returns></returns>
        List<T> GetDatas();

    }
}
