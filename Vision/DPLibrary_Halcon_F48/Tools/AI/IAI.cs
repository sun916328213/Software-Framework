using DPLibrary_Halcon_F48.Tools.AI.Definition;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DPLibrary_Halcon_F48.Tools.AI
{
    public interface IAI
    {
        /// <summary>
        /// 初始化模型
        /// </summary>
        /// <param name="path"></param>
        void InitModel(string path, int type);
        /// <summary>
        /// 推理
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        List<InferResultInfo> Inference(HObject image);
        /// <summary>
        /// 推理参数
        /// </summary>
        InferParam InferParam { get; set; }
    }
}
