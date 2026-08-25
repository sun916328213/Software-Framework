using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Vision.InvI
{
    public enum ModelType
    {
        Classification,
        Segmentation,
        Detection,
        UNKNOWN
    }

    /// <summary>
    /// InvI tool 管理类 此部分目前只针对存储离线检测模型的管理
    /// </summary>
    public class InvIToolMgr 
    {
        /**********************************************************************************/
        private const string Lib_AI_Path = "InvI.dll";
        //private const string Lib_AI_Path = "H:\\Project\\Infer\\Demo\\c++_dll\\x64\\Release\\InvI.dll";
        /// <summary>
        /// 返回轮廓点
        /// </summary>
        public struct Point
        {
            public int x;
            public int y;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct Bbox
        {
            public int x, y, w, h;
            public int id;
            public int point_num;  // 轮廓点的数量
            public float area;
            public float confidence;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200)]
            public string class_name;
            // public Point contours;      // 轮廓点数据单独接收
        }

        [DllImport(Lib_AI_Path, SetLastError = true)]
        static extern bool CreateInferAPI(string modelPath, ModelType type);

        // 执行分类方法
        [DllImport(Lib_AI_Path, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern IntPtr Class_Execute(ref int BboxNum,
            string modelPath, int width, int height, int channels, IntPtr data_r, IntPtr data_g, IntPtr data_b);

        /// <summary>
        /// vecBboxsPtr      代表返回的Bbox的指针起始地址
        /// BboxNum          代表返回的Bbox的数量
        /// vecPointsPtr     代表返回的Bbox中contours的起始地址
        /// </summary>
        [DllImport(Lib_AI_Path, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        static extern IntPtr Seg_Execute(ref int BboxNum, ref IntPtr vecPointsPtr,
            string modelPath, int width, int height, int channels, IntPtr data_r, IntPtr data_g, IntPtr data_b);

        // 析构
        [DllImport(Lib_AI_Path, SetLastError = true)]
        static extern void DestoryInfer(string modelPath, ModelType type);
        /**********************************************************************************/

        /// <summary>
        /// 接受推理结果的结构体,并把之前接受到的数据组装
        /// </summary>
        public struct InferResult
        {
            public int x, y, w, h;
            public int id;
            public float area;
            public float confidence;
            public string class_name;
            public List<Point> contours; // 轮廓点
        }


        //TODO:有bug，当模型被删除之后，需要再这个map中删除掉。
        //clear使用位置

        /// <summary>
        /// 将分割，分类，检测模型都分开存储,目的是界面显示和选取时根据不同的模型进行选择
        /// key是某个模型加载的路径,IntPtr 对应模型调用的指针
        /// </summary>
        List<string> InvI_Seg_Model = new List<string>();
        List<string> InvI_Cls_Model = new List<string>();
        List<string> InvI_Det_Model = new List<string>();

        /// <summary>
        /// 确定当前路径加载的InvI模型是否在使用
        /// </summary>
        private Dictionary<string, bool> m_dictInvIUsing = new Dictionary<string, bool>();

        /// <summary>
        /// 返回分割模型的所有接口
        /// </summary>
        public List<string> DictPath_InvISegClient
        {
            get
            {
                return InvI_Seg_Model;
            }
        }

        /// <summary>
        /// 返回分类模型的所有接口
        /// </summary>
        public List<string> DictPath_InvIClsClient
        {
            get
            {
                return InvI_Cls_Model;
            }
        }

        /// <summary>
        /// 返回目标检测模型的所有接口
        /// </summary>
        public List<string> DictPath_InvIDetClient
        {
            get
            {
                return InvI_Det_Model;
            }
        }

        /// <summary>
        /// 新增模型,模型路径为key，加载的模型指针为Value
        /// 添加格式是模型类型，加模型路径
        /// </summary>
        /// <param name="strModelPath">添加InvI模型的路径</param>
        public bool AddInvIClient(string strModelPath, ModelType type)
        {
            if (string.IsNullOrWhiteSpace(strModelPath))
            {

                throw new Exception("path is null strModelPath:" + strModelPath);
            }

            switch (type)
            {
                case ModelType.Classification:
                    {
                        if (!InvI_Cls_Model.Contains(strModelPath))
                        {
                            if (!CreateInferAPI(strModelPath, type))
                            {
                                  throw new Exception("Classification CreateInferAPI failed strModelPath:" + strModelPath);
                            }
                            else
                            {
                               // MessageBox.Show("Classification CreateInferAPI Success strModelPath:" + strModelPath);
                                InvI_Cls_Model.Add(strModelPath);
                                m_dictInvIUsing.Add(strModelPath, false);
                                return true;
                            }
                        }
                        break;
                    }
                case ModelType.Segmentation:
                    {
                        try
                        {
                            if (!InvI_Seg_Model.Contains(strModelPath))
                            {
                                if (!CreateInferAPI(strModelPath, type))
                                {
                                      throw new Exception("Segmentation CreateInferAPI failed strModelPath:" + strModelPath);
                                }
                                else
                                {
                                   // MessageBox.Show("Segmentation CreateInferAPI Success strModelPath:" + strModelPath);
                                    InvI_Seg_Model.Add(strModelPath);
                                    m_dictInvIUsing.Add(strModelPath, false);
                                    return true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                              throw new Exception(string.Format(" ModelType.Segmentation failed! ex:{0}", ex));
                        }
                        break;
                    }
                case ModelType.Detection:
                    {
                        try
                        {
                            if (!InvI_Det_Model.Contains(strModelPath))
                            {
                                if (CreateInferAPI(strModelPath, type))
                                {
                                      throw new Exception("Detection CreateInferAPI failed strModelPath:" + strModelPath);
                                }
                                else
                                {
                                   // MessageBox.Show("Detection CreateInferAPI Success strModelPath:" + strModelPath);
                                    InvI_Det_Model.Add(strModelPath);
                                    m_dictInvIUsing.Add(strModelPath, false);
                                    return true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                              throw new Exception(string.Format(" ModelType.Segmentation failed! ex:{0}", ex));
                        }
                        break;
                    }
            }
            return false;
        }

        public bool CheckInvIModelStatus(string strModelPath, ModelType type)
        {
            switch (type)
            {
                case ModelType.Classification:
                    {
                        if (InvI_Cls_Model.Contains(strModelPath))
                        {
                            return true;
                        }
                        break;
                    }
                case ModelType.Segmentation:
                    {
                        if (InvI_Seg_Model.Contains(strModelPath))
                        {
                            return true;
                        }
                        break;
                    }
                case ModelType.Detection:
                    {
                        if (InvI_Det_Model.Contains(strModelPath))
                        {
                            return true;
                        }
                        break;
                    }
            }
            return false;
        }

        /// <summary>
        /// 判断当前路径的InvI模型是否正在被使用
        /// </summary>
        /// <param name="strModelPath"></param>
        /// <returns></returns>
        public bool GetInvIIsUsing(string strModelPath, ModelType type)
        {
            switch (type)
            {
                case ModelType.Classification:
                    {
                        if (InvI_Cls_Model.Contains(strModelPath))
                        {
                            return m_dictInvIUsing[strModelPath];
                        }
                        break;
                    }
                case ModelType.Segmentation:
                    {
                        if (InvI_Seg_Model.Contains(strModelPath))
                        {
                            return m_dictInvIUsing[strModelPath];
                        }
                        break;
                    }
                case ModelType.Detection:
                    {
                        if (InvI_Det_Model.Contains(strModelPath))
                        {
                            return m_dictInvIUsing[strModelPath];
                        }
                        break;
                    }
            }
              throw new Exception(string.Format("{0} Model not added! ", strModelPath));
        }

        /// <summary>
        ///  设置当前模型是否在使用
        /// </summary>
        /// <param name="strModelPath"></param>
        /// <returns></returns>
        public void SetInvIIsUsing(string strModelPath, ModelType type, bool bUsing)
        {
            switch (type)
            {
                case ModelType.Classification:
                    {
                        if (InvI_Cls_Model.Contains(strModelPath))
                        {
                            m_dictInvIUsing[strModelPath] = bUsing;
                        }
                        break;
                    }
                case ModelType.Segmentation:
                    {
                        if (InvI_Seg_Model.Contains(strModelPath))
                        {
                            m_dictInvIUsing[strModelPath] = bUsing;
                        }
                        break;
                    }
                case ModelType.Detection:
                    {
                        if (InvI_Det_Model.Contains(strModelPath))
                        {
                            m_dictInvIUsing[strModelPath] = bUsing;
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 执行推理过程
        /// </summary>
        public List<InferResult> Infer_Execute(string modelName, ModelType type, int width, int height, int channels, IntPtr data_r,
            IntPtr data_g, IntPtr data_b)
        {
            List<InferResult> list_Infer_Result = new List<InferResult>();
            switch (type)
            {
                case ModelType.Classification:
                    {
                       // MessageBox.Show(string.Format("Classification  Infer_Execute start! modelName:{0}", modelName));
                        int BboxNum = 0;
                        IntPtr vecBboxsPtr = IntPtr.Zero;
                        try
                        {
                            vecBboxsPtr = Class_Execute(ref BboxNum, modelName, width, height, channels, data_r, data_g, data_b);
                            if ((vecBboxsPtr != IntPtr.Zero) && (BboxNum != 0))
                            {
                                for (int val = 0; val < BboxNum; val++)
                                {
                                    IntPtr ptr = (IntPtr)(vecBboxsPtr.ToInt64() + val * Marshal.SizeOf(typeof(Bbox)));
                                    var bbox_value = (Bbox)Marshal.PtrToStructure(ptr, typeof(Bbox));

                                    // InferResult 方式接受
                                    InferResult infer_result = new InferResult();
                                    infer_result.x = bbox_value.x;
                                    infer_result.y = bbox_value.y;
                                    infer_result.w = bbox_value.w;
                                    infer_result.h = bbox_value.h;
                                    infer_result.id = bbox_value.id;
                                    infer_result.area = bbox_value.area;
                                    infer_result.class_name = bbox_value.class_name;
                                    infer_result.confidence = bbox_value.confidence;
                                    list_Infer_Result.Add(infer_result);

                                }
                            }
                        }
                        finally
                        {
                            //if ( vecBboxsPtr != IntPtr.Zero )
                            //    Marshal.FreeHGlobal(vecBboxsPtr);   
                        }
                      //    throw new Exception(string.Format("Classification  Infer_Execute Success! modelName:{0}", modelName));
                        break;
                    }
                case ModelType.Segmentation:
                    {
                       // MessageBox.Show(string.Format("Segmentation  Infer_Execute start! modelName:{0}", modelName));
                        int BboxNum = 0;
                        IntPtr vecBboxsPtr = IntPtr.Zero;
                        IntPtr vecPointsPtr = IntPtr.Zero;
                        try
                        {
                            vecBboxsPtr = Seg_Execute(ref BboxNum, ref vecPointsPtr, modelName, width, height, channels, data_r, data_g, data_b);
                            if ((vecBboxsPtr != IntPtr.Zero) && (BboxNum != 0))
                            {
                                // 1.获取所有的Bbox信息
                                IntPtr pointPtr = vecPointsPtr;
                                for (int val = 0; val < BboxNum; val++)
                                {
                                    IntPtr ptr = (IntPtr)(vecBboxsPtr.ToInt64() + val * Marshal.SizeOf(typeof(Bbox)));
                                    var bbox_value = (Bbox)Marshal.PtrToStructure(ptr, typeof(Bbox));

                                    // InferResult 方式接受
                                    InferResult infer_result = new InferResult();
                                    infer_result.x = bbox_value.x;
                                    infer_result.y = bbox_value.y;
                                    infer_result.w = bbox_value.w;
                                    infer_result.h = bbox_value.h;
                                    infer_result.id = bbox_value.id;
                                    infer_result.area = bbox_value.area;
                                    infer_result.class_name = bbox_value.class_name;
                                    infer_result.confidence = bbox_value.confidence;

                                    // 添加轮廓点信息 获取轮廓点数据时耗时
                                    List<Point> list_point = new List<Point>();
                                    for (int point_index = 0; point_index < bbox_value.point_num; point_index++)
                                    {
                                        IntPtr Ptr = (IntPtr)(pointPtr.ToInt64() + point_index * Marshal.SizeOf(typeof(Point)));
                                        var point_value = (Point)Marshal.PtrToStructure(Ptr, typeof(Point));
                                        //LogHelper.Info(" Segmentation list_point: [ x: " + point_value.x + " y: " + point_value.y + " ] ");
                                        list_point.Add(point_value);
                                        if ((bbox_value.point_num - 1) == point_index)
                                        {
                                            pointPtr = Ptr + Marshal.SizeOf(typeof(Point));  // 赋为下一个地址
                                        }
                                    }
                                    infer_result.contours = list_point;
                                    list_Infer_Result.Add(infer_result);
                                }
                            }
                        }
                        finally
                        {
                            //if ( vecBboxsPtr != IntPtr.Zero  )
                            //    Marshal.FreeHGlobal(vecBboxsPtr);
                            //if ( vecPointsPtr != IntPtr.Zero )
                            //    Marshal.FreeHGlobal(vecPointsPtr);
                        }
                     //     throw new Exception(string.Format("Segmentation  Infer_Execute Success! modelName:{0}", modelName));
                        break;
                    }
                case ModelType.Detection:
                    {

                        break;
                    }
            }
            return list_Infer_Result;
        }

        /// <summary>
        /// 删除InvI模型，如果当前路径对应的模型，在该程序中数量为0 ，就删除该模型
        /// </summary>
        /// <param name="modelPath"></param>
        public void DeleteInvICount(string modelPath, ModelType type)
        {
            try
            {
                if (string.IsNullOrEmpty(modelPath))
                {
                    return;
                }

                DestoryInfer(modelPath, type);
                switch (type)
                {
                    case ModelType.Classification:
                        InvI_Cls_Model.Remove(modelPath);
                        break;
                    case ModelType.Segmentation:
                        InvI_Seg_Model.Remove(modelPath);
                        break;
                    case ModelType.Detection:
                        InvI_Det_Model.Remove(modelPath);
                        break;
                }
            }
            catch
            {
                  throw new Exception(string.Format("InviToolMgr Description Failed to delete the model file! modelPath:{0}", modelPath));
            }
              throw new Exception(string.Format("InviToolMgr  Delete Success!  modelPath:{0}", modelPath));
        }
    }
}
