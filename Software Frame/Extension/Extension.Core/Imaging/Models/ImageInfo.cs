// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：ImageInfo、ImageInfoDTO。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using System.Collections.Generic;

namespace Extension.Imaging.Models
{
    /// <summary>
    /// ImageInfo 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public struct ImageInfo
    {
        public string ID { get; set; }

        public List<string> ImagePath { get; set; }
    }

    /// <summary>
    /// ImageInfoDTO 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// </summary>
    public struct ImageInfoDTO
    {
        public string ID { get; set; }

        public int ImageCount { get; set; }
    }
}