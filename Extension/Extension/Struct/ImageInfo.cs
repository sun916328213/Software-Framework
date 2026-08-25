using Mapster;
using System.Collections.Generic;

namespace Extension.Struct
{
    public struct ImageInfo
    {
        public string ID { get; set; }

        public List<string> ImagePath { get; set; }
    }

    public struct ImageInfoDTO
    {
        public string ID { get; set; }

        public int ImageCount { get; set; }
    }
    
    /// <summary>
    /// SImageInfo相关映射配置
    /// </summary>
    public static class  ImageInfoMapping
    {
        /// <summary>
        /// 配置SImageInfo和SImageInfoDTO之间的映射关系
        /// </summary>
        /// <param name="config">映射配置对象</param>
        public static void Configure(TypeAdapterConfig config)
        {
            // 配置从SImageInfo到SImageInfoDTO的映射
            config.ForType<ImageInfo, ImageInfoDTO>()
                .Map(dest => dest.ImageCount, src => src.ImagePath != null ? src.ImagePath.Count : 0);
                
            // 从SImageInfoDTO到SImageInfo的映射使用默认映射
            config.ForType<ImageInfoDTO, ImageInfo>()
                .Ignore(dest => dest.ImagePath); // DTO不包含ImagePath的详细信息
        }
    }
}