using Extension.Core.EnumEx;
using Extension.Core.FileEx;
using Extension.Tool;
using Prism.Ioc;
using Secs4Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Secs.Variable
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

    public abstract class VIDBase :  DataBase<VIDInfo>
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public override string FilePath { get; set; } = "Config//SecsJson//VID";
   
        public override string FileName { get; set; }

        public override ConcurrentList<VIDInfo> Datas { get; set; } =new ConcurrentList<VIDInfo>();

     

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