using System.Collections.Generic;

namespace Extension.Imaging.Models
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
}