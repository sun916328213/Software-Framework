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

        /// <summary>
        /// 所属表：SVID / DVID / ECID。由 <see cref="VIDBase.Category"/> 在加载时写入。
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 带类别前缀的展示用 ID，形如 <c>SVID:102</c>。
        /// <para>
        /// 只用于诊断信息和界面显示 —— 索引与报表引用都用裸 ID，前提是 ID 跨表唯一
        /// （由 <c>VIDManager.Load</c> 强制校验，重号直接抛）。
        /// </para>
        /// </summary>
        public string QualifiedId => string.IsNullOrEmpty(Category) ? ID : $"{Category}:{ID}";

        /// <summary>
        /// 把 "SVID:102" 这种带类别前缀的写法也认下来，返回裸 ID。
        /// 报表/配置里两种写法都能用，避免手工粘贴带前缀的引用直接失效。
        /// </summary>
        public static string NormalizeId(string id)
        {
            var trimmed = (id ?? string.Empty).Trim();
            var colon = trimmed.IndexOf(':');
            return colon < 0 ? trimmed : trimmed[(colon + 1)..];
        }
    }

    /// <summary>
    /// VIDBase 的类型定义。SecsGem 模块：SECS/GEM 契约、协议运行及界面适配；状态变化和报文处理以协议实现及配置为准。
    /// 继承或实现的契约：DataBase&lt;VIDInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public abstract class VIDBase :  DataBase<VIDInfo>
    {
        /// <summary>
        /// 文件路径由子类指定（SVID/DVID/ECID 各一个目录），基类不再给默认目录。
        /// </summary>

        public override string FileName { get; set; }

        /// <summary>本表的名字，SVID/DVID/ECID。VID 的 ID 只在本表内唯一。</summary>
        public abstract string Category { get; }

        public override ConcurrentList<VIDInfo> Datas { get; set; } =new ConcurrentList<VIDInfo>();



        /// <summary>
        /// 执行加载操作；对应入口为 Load。
        /// <para>
        /// 修复：原实现文件缺失时 throw "File Not Exist!!"，Config() 整条失败，
        /// 且默认项无从载入。现在交给基类兜底（见 <see cref="DataBase{T}.Load"/>），这里只补 DataType 与分类。
        /// </para>
        /// </summary>
        public override void Load()
        {
            base.Load();

            foreach (var item in Datas)
            {
                item.DataType = EnumEx.GetEnum<SecsFormat>(item.DataTypeName);
                item.Category = Category;
            }
        }
 
    }
}