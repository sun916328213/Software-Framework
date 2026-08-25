using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DPLibrary_Halcon_F48.Tools.AI.Invi
{
    public enum InviType
    {
            Classification,
            Segmentation,
            Detection,
            UNKNOWN
        
    }

    public class InviResult
    {
        public int class_id { get; set; }
        public string class_name { get; set; }
        public double  area { get; set; }
        public double confidence { get; set; }
        public Rect bd_rect { get; set; }
        public List<PointD> contour_points { get; set; }
    }

    public class Rect
    { 
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class PointD
    {
        public int x { get; set; }
        public int y { get; set; }
    }

}
