// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：ImageInfoMapping。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Extension.Imaging.Models、Mapster。导入不代表所有类型都会在运行时加载。
using Extension.Imaging.Models;
using Mapster;

namespace Extension.Integration.Mapster
{
    /// <summary>配置图像模型与其数据传输对象之间的映射关系。</summary>
    public static class ImageInfoMapping
    {
        /// <summary>
        /// 配置当前对象或组合入口；对应入口为 Configure。
        /// 正文中的关键协作调用包括 Map、ForType、Ignore；具体条件和先后次序见方法体。
        /// </summary>
        public static void Configure(TypeAdapterConfig config)
        {
            config.ForType<ImageInfo, ImageInfoDTO>()
                .Map(dest => dest.ImageCount, src => src.ImagePath != null ? src.ImagePath.Count : 0);

            config.ForType<ImageInfoDTO, ImageInfo>()
                .Ignore(dest => dest.ImagePath);
        }
    }
}