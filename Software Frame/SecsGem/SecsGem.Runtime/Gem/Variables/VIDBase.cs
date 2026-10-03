// 【中文阅读说明】
// SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
// 本文件定义：VIDInfo、VIDBase。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：SecsGem.Core、SecsGem.Runtime、Extension.Core.Enums、Extension.IO.Files、Extension.IO.Csv、Extension.IO.Retention。导入不代表所有类型都会在运行时加载。
using SecsGem.Core;
using SecsGem.Runtime;
using Extension.Core.Enums;
using Extension.IO.Files;
using Extension.IO.Csv;
using Extension.IO.Retention;
using Extension.Core.Collections;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SecsGem.Runtime.Variable
{
    /// <summary>
    ///  VID数据结构体
    /// </summary>
    public class VIDInfo
    {
        /// <summary>
        /// SVID唯一标识符
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// 名字
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 数据值
        /// </summary>
        public object Value { get; set; }
        /// <summary>
        /// 数据类型名称
        /// </summary>
        public string DataTypeName { get; set; }
        /// <summary>
        /// 数据类型Secs格式
        /// </summary>
        public SecsFormat DataType { get; set; }
    }

    /// <summary>
    /// VIDBase 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：DataBase&lt;VIDInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class VIDBase :  DataBase<VIDInfo>
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public override string FilePath { get; set; } = "Config//SecsJson//VID";
   
        public override string FileName { get; set; }

        public override ConcurrentList<VIDInfo> Datas { get; set; } =new ConcurrentList<VIDInfo>();

     

        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// 正文中的关键协作调用包括 Combine、Exists、ReadCsv、Clear、GetEnum；具体条件和先后次序见方法体。
        /// </summary>
        public override void Load()
        {
            string path = Path.Combine(FilePath, FileName);
            if (!File.Exists(path)) throw new Exception("File Not Exist!!");

            var datas = CSVEx.ReadCsv<VIDInfo>(path);

            Datas.Clear();
            foreach (var item in datas)
            {
                item.DataType = EnumEx.GetEnum<SecsFormat>(item.DataTypeName);
                Datas.Add(item);
            }    
        }
 
    }
}