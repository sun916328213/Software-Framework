using Extension.Imaging.Models;
using Mapster;

namespace Extension.Integration.Mapster
{
    /// <summary>配置图像模型与其数据传输对象之间的映射关系。</summary>
    public static class ImageInfoMapping
    {
        public static void Configure(TypeAdapterConfig config)
        {
            config.ForType<ImageInfo, ImageInfoDTO>()
                .Map(dest => dest.ImageCount, src => src.ImagePath != null ? src.ImagePath.Count : 0);

            config.ForType<ImageInfoDTO, ImageInfo>()
                .Ignore(dest => dest.ImagePath);
        }
    }
}