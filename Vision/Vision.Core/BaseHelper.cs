using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    public static class LiangBarskyClip
    {
        public static bool Clip(
            double xmin, double xmax, double ymin, double ymax,
            ref double x1, ref double y1, ref double x2, ref double y2)
        {
            double dx = x2 - x1, dy = y2 - y1;
            double t0 = 0.0, t1 = 1.0;

            bool ClipEdge(double p, double q)
            {
                if (Math.Abs(p) < 1e-12) return q >= 0;     // 平行：在内则保留
                double t = q / p;
                if (p < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
                return true;
            }

            if (!ClipEdge(-(dx), x1 - xmin)) return false; // x >= xmin
            if (!ClipEdge(+(dx), xmax - x1)) return false; // x <= xmax
            if (!ClipEdge(-(dy), y1 - ymin)) return false; // y >= ymin
            if (!ClipEdge(+(dy), ymax - y1)) return false; // y <= ymax

            x2 = x1 + t1 * dx; y2 = y1 + t1 * dy;
            x1 = x1 + t0 * dx; y1 = y1 + t0 * dy;
            return true;
        }
    }

    public static class BaseHelper 
    {

        /// <summary>
        /// 判断文件夹是否存在,不存在则创建
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool JudgeDirectoryExist(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return true;
        }
    }
}
