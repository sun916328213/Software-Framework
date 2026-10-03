// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：LiangBarskyClip、BaseHelper。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.IO、System.Linq、System.Text、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vision.Core
{
    /// <summary>
    /// LiangBarskyClip 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public static class LiangBarskyClip
    {
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 Clip。
        /// 正文中的关键协作调用包括 Abs、ClipEdge；具体条件和先后次序见方法体。
        /// </summary>
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

    /// <summary>
    /// BaseHelper 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
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
