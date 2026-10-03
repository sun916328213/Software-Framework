// 【中文阅读说明】
// Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
// 本文件定义：TeArrLen、TeStatus、TeImage、TeToolType、TeString、TeSizeForNode、TeSize、TeRotatedRect、TeRect、TePoint、TeObjectInSrc、TeNodeRst、TeNodeRstDyn、TePredictParam、TeColor、TeNodeParamInfo、TeTree。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Runtime.InteropServices、System.Text。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Vision.AI.Aiditron.TeDemo
{
    /// <summary>
    /// TeArrLen 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public enum TeArrLen
    {
        MAX_CLASS_NUM = 256,
        MAX_OBJECT_NUM = 4096,
        MAX_RTINFO_NUM = 64,
        MAX_TAG_LENGTH = 8,
        MAX_NODE_NUM = 64,
        TE_NAME_STRING_LEN = 256,
        MAX_POINT_NUM = 4,
        MAX_CHANNEL_NUM = 32
    };

    /// <summary>
    /// TeStatus 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public enum TeStatus
    {
        TE_SUCCESS = 0,  //执行成功。
        TE_DEVICE_NOT_SUPPORTED = TE_SUCCESS + 1,//设备不支持
        TE_INVALID_ARGUMENT = TE_DEVICE_NOT_SUPPORTED + 1,//参数无效
        TE_OUT_OF_MEMORY = TE_INVALID_ARGUMENT + 1,//显存不足
        TE_LOAD_MODEL_FAILED = TE_OUT_OF_MEMORY + 1,//加载模型失败
        TE_OUT_OF_DETECT_RANGE = TE_LOAD_MODEL_FAILED + 1,//超出检测范围
        TE_CURRENT_GPU_BUSY = TE_OUT_OF_DETECT_RANGE + 1,//当前要调用的GPU设备处于繁忙状态
        TE_STATUS_CHIP_NOT_SUPPORTED = TE_CURRENT_GPU_BUSY + 1,//当前要调用的模块 加密芯片不支持
        TE_DLL_NOT_MATCH_MODEL = TE_STATUS_CHIP_NOT_SUPPORTED + 1, //调用的模型与DLL不匹配
        TE_DLLS_INCOMPATIBLE = TE_DLL_NOT_MATCH_MODEL + 1, //调用的DLL不一致
        TE_OUT_OF_CPU_MEMORY = TE_DLLS_INCOMPATIBLE + 1,//内存不足
        TE_ERROR_PATH = TE_OUT_OF_CPU_MEMORY + 1, //传入错误的路径 导致无法找到指定文件
        TE_COMPILER_NOT_ALLOWED_TO_RUN = TE_ERROR_PATH + 1, //release模式下不允许编译器运行
    };

    //图像格式
    public struct TeImage
    {
        public int nChannels; //通道数
        public int iImgWidth;  //图像宽
        public int iImgHeight; //图像高
        public int iWidthStep;  //步长
        public IntPtr pImgData; //图像数据

    };

    //节点类型
    public enum TeToolType
    {
        E_Location_Tool = 0, //对象定位节点
        E_PixelDetect_Tool = E_Location_Tool + 1, //像素分割节点
        E_Classify_Tool = E_PixelDetect_Tool + 1, //场景分类节点
        E_Total_Tool = E_Classify_Tool + 1 //包含上述所有
    }

    //字符串格式
    public struct TeString
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.TE_NAME_STRING_LEN))]
        private byte[] pData;

        public string pString
        {
            get
            {
                if (pData == null)
                    pData = new byte[(int)(TeArrLen.TE_NAME_STRING_LEN)];
                int iPos = 0;
                while (iPos < (int)(TeArrLen.TE_NAME_STRING_LEN) && pData[iPos + 1] != 0) iPos++;
                return Encoding.Default.GetString(pData, 0, iPos + 1);
            }
            set
            {
                pData = new byte[(int)(TeArrLen.TE_NAME_STRING_LEN)];
                byte[] pb = Encoding.Default.GetBytes(value);
                Array.Copy(pb, pData, pb.Length < (int)(TeArrLen.TE_NAME_STRING_LEN) ? pb.Length : (int)(TeArrLen.TE_NAME_STRING_LEN));
            }
        }
    };

    //节点宽高（初始化中用于为指定节点分配指定大小的资源）
    public struct TeSizeForNode
    {
        public int iMaxWidth; //最大宽
        public int iMaxHeight; //最大高
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.TE_NAME_STRING_LEN))]
        public byte[] strNodeName; //节点名
    }

    //Size结构体
    public struct TeSize
    {
        int width;
        int height;

        /// <summary>
        /// 初始化 TeSize 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public TeSize(int iWidth, int iHeight)
        {
            width = iWidth;
            height = iHeight;
        }
    };

    /// <summary>
    /// TeRotatedRect 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public struct TeRotatedRect
    {
        public float fCenterX; //中心点X坐标
        public float fCenterY; //中心点Y坐标
        public float fAngle; //角度
        public float fWidth; //宽
        public float fHeight; //高
    }



    //矩形信息
    public struct TeRect
    {
        public int x; //x坐标 左上角
        public int y; //y坐标
        public int width; //矩形宽
        public int height; //矩形高
    };

    /// <summary>
    /// TePoint 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TePoint
    {
        public int iX;
        public int iY;
    }


    //目标信息
    [StructLayout(LayoutKind.Sequential)]
    public struct TeObjectInSrc
    {
        public TeRect stPosi; //目标外接矩形
        public float fArea; //目标的面积
        public float fProb; //目标置信度
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_TAG_LENGTH))]
        public byte[] pstrTag; //目标标签
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_POINT_NUM))]
        public TePoint[] stPoints;
    };

    //一个节点的预测结果
    [StructLayout(LayoutKind.Sequential)]
    public struct TeNodeRst
    {
        public int iNodeID; //节点ID
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.TE_NAME_STRING_LEN))]
        public byte[] strNodeName; //节点名
        public TeToolType eNodeType; //节点类型
        public int iObjNum; //预测到的目标个数
        public int iLabelNum; //目标种类数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_OBJECT_NUM))]
        public TeObjectInSrc[] pstObjectInfo; //目标信息
        public IntPtr pnLabelMat; //标签图 （仅对像素分割模块有效）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_CLASS_NUM) * (int)(TeArrLen.MAX_TAG_LENGTH))]
        public byte[] pcLabelList;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 createBuffer。
        /// 正文中的关键协作调用包括 AllocHGlobal；具体条件和先后次序见方法体。
        /// </summary>
        public void createBuffer(TeToolType eNodeType_in, int iMaxImageWidth, int iMaxImageHeight)
        {
            pnLabelMat = IntPtr.Zero;

            if (TeToolType.E_PixelDetect_Tool == eNodeType_in)
            {
                pnLabelMat = Marshal.AllocHGlobal(sizeof(byte) * iMaxImageWidth * iMaxImageHeight);
            }

        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 releaseBuffer。
        /// 正文中的关键协作调用包括 FreeHGlobal；具体条件和先后次序见方法体。
        /// </summary>
        public void releaseBuffer()
        {
            if (IntPtr.Zero != pnLabelMat)
            {
                Marshal.FreeHGlobal(pnLabelMat);
                pnLabelMat = IntPtr.Zero;
            }
        }

    };


    //一个节点的预测结果
    [StructLayout(LayoutKind.Sequential)]
    public struct TeNodeRstDyn
    {
        public int iNodeID; //节点ID
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.TE_NAME_STRING_LEN))]
        public byte[] strNodeName; //节点名
        public TeToolType eNodeType; //节点类型
        public int iObjNum; //预测到的目标个数
        public int iLabelNum; //目标种类数
        public int iMaxObjNum; //
        public IntPtr pstObjectInfo; //目标信息
        public IntPtr pnLabelMat; //标签图 （仅对像素分割模块有效）
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_CLASS_NUM) * (int)(TeArrLen.MAX_TAG_LENGTH))]
        public byte[] pcLabelList;

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 createBuffer。
        /// 正文中的关键协作调用包括 AllocHGlobal、SizeOf；具体条件和先后次序见方法体。
        /// </summary>
        public void createBuffer(TeToolType eNodeType_in, int iMaxObjNum_in, int iMaxImageWidth, int iMaxImageHeight)
        {
            iMaxObjNum = 1 > iMaxObjNum_in ? 1 : iMaxObjNum_in;
            pstObjectInfo = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TeObjectInSrc)) * iMaxObjNum);
            pnLabelMat = IntPtr.Zero;

            if (TeToolType.E_PixelDetect_Tool == eNodeType_in)
            {
                pnLabelMat = Marshal.AllocHGlobal(sizeof(byte) * iMaxImageWidth * iMaxImageHeight);
            }

        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 releaseBuffer。
        /// 正文中的关键协作调用包括 FreeHGlobal；具体条件和先后次序见方法体。
        /// </summary>
        public void releaseBuffer()
        {
            if (IntPtr.Zero != pnLabelMat)
            {
                Marshal.FreeHGlobal(pnLabelMat);
                pnLabelMat = IntPtr.Zero;
            }
            if (IntPtr.Zero != pstObjectInfo)
            {
                Marshal.FreeHGlobal(pstObjectInfo);
                pstObjectInfo = IntPtr.Zero;
            }
        }
    };




    //预测约束参数（像素分割使用）
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct TePredictParam
    {
        public float fProbThre; //概率阈值
        public int nAreaThre; //面积阈值
        public IntPtr pbUnprocessed; //掩膜
    };

    //自定义颜色
    [StructLayout(LayoutKind.Sequential)]
    public struct TeColor
    {
        public byte Red; //红色通道
        public byte Green; //绿色通道
        public byte Blue; //蓝色通道
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = (int)(TeArrLen.MAX_TAG_LENGTH))]
        public string pstrLabelName; //标签名
    }

    /// <summary>
    /// TeNodeParamInfo 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TeNodeParamInfo
    {
        public TeToolType eToolType;

        public int iImageLayerCnt;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_CHANNEL_NUM))]
        public int[] piImageLayerChannel;

        public int iLabelCnt;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)(TeArrLen.MAX_CLASS_NUM) * (int)(TeArrLen.MAX_TAG_LENGTH))]
        public byte[] pcLabelList;

        public string[] strLabelList
        {
            get
            {
                string[] strRetList = new string[iLabelCnt];

                for (int i = 0; i < iLabelCnt; i++)
                {
                    int iStartPos = i * ((int)(TeArrLen.MAX_TAG_LENGTH));
                    int iEndFindPos = iStartPos + ((int)(TeArrLen.MAX_TAG_LENGTH));
                    int iEndPos = iStartPos;
                    while (iEndPos < iEndFindPos && pcLabelList[iEndPos + 1] != 0) iEndPos++;

                    strRetList[i] = Encoding.Default.GetString(pcLabelList, iStartPos, iEndPos - iStartPos + 1);
                }

                return strRetList;
            }
        }
    }

    /// <summary>
    /// TeTree 的类型定义。Vision 模块：视觉工具模型、算法适配和编辑展示；厂商能力与界面转换应在对应边界内协作。
    /// </summary>
    public class TeTree
    {

#if DEBUG
        [DllImport("teTreePredictd.dll", EntryPoint = "teInitProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitProj(ref IntPtr pnProjHandle, ref TeString pstProjPath, TeString[] pstrResultNodeName, TeSize stMaxSize, int iResultNodeNum, int iDevID);

        [DllImport("teTreePredictd.dll", EntryPoint = "tePredictProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus tePredictProj(IntPtr nProjHandle, [In, MarshalAs(UnmanagedType.LPArray)]TeImage[] pSrcImage, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeNodeRst[] pstNodeRst, int iResultNodeNum, [In, MarshalAs(UnmanagedType.LPArray)]TePredictParam[] pstPredictParam = null, TeString[] pstrResultNodeName = null, bool bSort = false);

        [DllImport("teTreePredictd.dll", EntryPoint = "tePredictProjDyn", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus tePredictProj(IntPtr nProjHandle, [In, MarshalAs(UnmanagedType.LPArray)]TeImage[] pSrcImage, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeNodeRstDyn[] pstNodeRst, int iResultNodeNum, [In, MarshalAs(UnmanagedType.LPArray)]TePredictParam[] pstPredictParam = null, TeString[] pstrResultNodeName = null, bool bSort = false);

        [DllImport("teTreePredictd.dll", EntryPoint = "teReleaseProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseProj(IntPtr nProjHandle);

        [DllImport("teTreePredictd.dll", EntryPoint = "teDrawRstInSrcImageByFilling", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByFilling(ref TeImage pSrcImage, IntPtr pstNodeRst, TeColor[] pstString2Color);

        [DllImport("teTreePredictd.dll", EntryPoint = "teDrawRstInSrcImageByRect", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByRect(ref TeImage pSrcImage, IntPtr pstNodeRst, TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 2);

        [DllImport("teTreePredictd.dll", EntryPoint = "teGetError", CallingConvention = CallingConvention.Cdecl)]
        static extern void teGetError(TeStatus eStatus, ref TeString pstrError);

        [DllImport("teTreePredict.dll", EntryPoint = "teReadImageFromHD", CallingConvention = CallingConvention.Cdecl)]
        static extern TeImage teReadImageFromHD(ref TeString rstImgPath);

        [DllImport("teTreePredict.dll", EntryPoint = "teReleaseImage", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseImage(ref TeImage rstImg);

        [DllImport("teRuntimed.dll", EntryPoint = "teInitDll", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitDll(ref IntPtr nDllHandle);

        [DllImport("teRuntimed.dll", EntryPoint = "teReleaseTeDll", CallingConvention = CallingConvention.Cdecl)]
        static extern int teReleaseTeDll(ref IntPtr nDllHandle);

        [DllImport("teTreePredictd.dll", EntryPoint = "teGetNodeParamInfo", CallingConvention = CallingConvention.Cdecl)]
        static extern bool teGetNodeParamInfo(ref TeString pstProjFilePath, ref TeString pstNodeName, ref TeNodeParamInfo pstDstNodeInfo);

        [DllImport("teTreePredictd.dll", EntryPoint = "teSetRstAreaMapbackFlag", CallingConvention = CallingConvention.Cdecl)]

        static extern void teSetRstAreaMapbackFlag(IntPtr nProjHandle, bool bMapbackFlag);
#else 
        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teInitProj。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teInitProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitProj(ref IntPtr pnProjHandle, ref TeString pstProjPath, TeString[] pstrResultNodeName, TeSize stMaxSize, int iResultNodeNum, int iDevID);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 tePredictProj。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "tePredictProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus tePredictProj(IntPtr nProjHandle, [In, MarshalAs(UnmanagedType.LPArray)] TeImage[] pSrcImage, [Out, In, MarshalAs(UnmanagedType.LPArray)]TeNodeRst[] pstNodeRst, int iResultNodeNum, [In, MarshalAs(UnmanagedType.LPArray)]TePredictParam[] pstPredictParam = null, TeString[] pstrResultNodeName = null, bool bSort = false);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teReleaseProj。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teReleaseProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseProj(IntPtr nProjHandle);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teDrawRstInSrcImageByFilling。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teDrawRstInSrcImageByFilling", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByFilling(ref TeImage pSrcImage, IntPtr pstNodeRst, TeColor[] pstString2Color);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teDrawRstInSrcImageByRect。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teDrawRstInSrcImageByRect", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByRect(ref TeImage pSrcImage, IntPtr pstNodeRst, [In, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 2);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teGetError。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teGetError", CallingConvention = CallingConvention.Cdecl)]
        static extern void teGetError(TeStatus eStatus, ref TeString pstrError);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teReadImageFromHD。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teReadImageFromHD", CallingConvention = CallingConvention.Cdecl)]
        static extern TeImage teReadImageFromHD(ref TeString rstImgPath);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teReleaseImage。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teReleaseImage", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseImage(ref TeImage rstImg);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teInitDll。
        /// </summary>
        [DllImport("teRuntime.dll", EntryPoint = "teInitDll", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitDll(ref IntPtr nDllHandle);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teReleaseTeDll。
        /// </summary>
        [DllImport("teRuntime.dll", EntryPoint = "teReleaseTeDll", CallingConvention = CallingConvention.Cdecl)]
        static extern int teReleaseTeDll(ref IntPtr nDllHandle);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teGetNodeParamInfo。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teGetNodeParamInfo", CallingConvention = CallingConvention.Cdecl)]
        static extern bool teGetNodeParamInfo(ref TeString pstProjFilePath, ref TeString pstNodeName, ref TeNodeParamInfo pstDstNodeInfo);

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 teSetRstAreaMapbackFlag。
        /// </summary>
        [DllImport("teTreePredict.dll", EntryPoint = "teSetRstAreaMapbackFlag", CallingConvention = CallingConvention.Cdecl)]

        static extern void teSetRstAreaMapbackFlag(IntPtr nProjHandle, bool bMapbackFlag);


#endif


        IntPtr m_pPredictionDllHandle;  //tePredict(d).dll句柄
        IntPtr m_TreePredictHandle;  //预测树句柄
        TeString[] m_pstrOutputNodeName = new TeString[(int)TeArrLen.MAX_NODE_NUM];//需要输出的节点名字
        public int m_iOutputNodeNum = 0;//需要输出的节点个数

                     //初始化节点树
        public int teInitTree(ref TeString pstProjPath, ref List<TeString> pstrOutputNodeList, int iMaxWidth, int iMaxHeight)
        {
            try
            {
                int iRst = 0;
                switch (teInitDll(ref m_pPredictionDllHandle))//
                {

                    case TeStatus.TE_SUCCESS:
                        {
                            for (int i = 0; i < pstrOutputNodeList.Count; i++)
                            {
                                m_pstrOutputNodeName[i] = pstrOutputNodeList[i];
                            }
                            m_iOutputNodeNum = pstrOutputNodeList.Count;

                            TeSize stMaxSize = new TeSize(iMaxWidth, iMaxHeight);
                            TeStatus eStatus = teInitProj(ref m_TreePredictHandle, ref pstProjPath, m_pstrOutputNodeName, stMaxSize, m_iOutputNodeNum, 0);

                            if (eStatus != TeStatus.TE_SUCCESS)
                            {
                                TeString strErr = new TeString();
                                teGetError(eStatus, ref strErr);
                                throw new Exception("初始化项目中的节点失败");
                            }
                            break;

                        }
                    case TeStatus.TE_COMPILER_NOT_ALLOWED_TO_RUN:
                        {
                            throw new Exception("当前为release模式，请关闭visual studio，否则芯片可能会被锁死，tePrediction.dll 初始化失败");
                        }

                    default:
#if DEBUG
                        throw new Exception("tePredictiond.dll 初始化失败，请检查dll路径");
#else
                        throw new Exception("tePrediction.dll 初始化失败，请检查dll路径");
#endif
                }

                return iRst;
            }
            catch (System.Exception ex)
            {
                if (ex.Source != null)
                    throw new Exception("节点树初始化失败，IOException message: {0}");
                throw;
            }
        }


        //预测
        public int tePredict(ref TeImage pSrcImage, ref TeNodeRst[] pstNodeRstSet, ref TePredictParam[] pstParam)
        {
            //2023-09-06-接口修改
            //Author by:Amos
            //在2020.0313版本中，预测接口只预测单张图，这里做修改，以便兼容。
            TeImage[] pstIamgeArry = new TeImage[1];
            pstIamgeArry[0] = pSrcImage;

            TeStatus eStatus = tePredictProj(m_TreePredictHandle, pstIamgeArry, pstNodeRstSet, m_iOutputNodeNum, pstParam, m_pstrOutputNodeName);
            if (eStatus != TeStatus.TE_SUCCESS)
            {
                TeString strError = new TeString();
                teGetError(eStatus, ref strError);
                throw new Exception("项目预测失败");
            }

            return 0;
        }


        //对指定节点的预测结果作图
        public int teShowRstInSrcImage(ref TeImage pSrcImage, ref TeNodeRst pstNodeRst, TeColor[] pstString2Color = null, float fLabelSize = 0.5f, int iThickness = 2)
        {
            //if (null == pSrcBitmap)
            //{
            //    return -1;
            //}


            int iSize = Marshal.SizeOf(typeof(TeNodeRst));
            IntPtr intPtr = Marshal.AllocHGlobal(iSize);
            Marshal.StructureToPtr(pstNodeRst, intPtr, true);

            if (TeToolType.E_PixelDetect_Tool == pstNodeRst.eNodeType && IntPtr.Zero != pstNodeRst.pnLabelMat)
            {
                //以填充形式将结果在原图上画出
                teDrawRstInSrcImageByFilling(ref pSrcImage, intPtr, pstString2Color);
            }


            //以矩形框形式将结果在原图上画出
            teDrawRstInSrcImageByRect(ref pSrcImage, intPtr, pstString2Color, fLabelSize, iThickness);
            Marshal.FreeHGlobal(intPtr);
            return 0;
        }


        //释放节点树
        public int teReleaseTree()
        {
            if (IntPtr.Zero == m_TreePredictHandle || IntPtr.Zero == m_pPredictionDllHandle)
            {
                return -1;
            }

            try
            {
                teReleaseProj(m_TreePredictHandle);
                m_TreePredictHandle = IntPtr.Zero;
            }
            catch (System.Exception ex)
            {
                if (ex.Source != null)
                    throw new Exception("项目释放失败，IOException message: {0}");
                throw;
            }

            return 0;
        }

        //获取工程各个节点的信息
        public TeNodeParamInfo teGetNodeParamInfo(ref TeString pstProjFilePath, ref TeString pstNodeName)
        {
            TeNodeParamInfo pstDstNodeInfo = new TeNodeParamInfo();

            teGetNodeParamInfo(ref pstProjFilePath, ref pstNodeName, ref pstDstNodeInfo);

            return pstDstNodeInfo;
        }

        //从硬盘载入图片
        public TeImage[] teReadImages(TeString[] rstImgPath)
        {
            TeImage[] teimage = new TeImage[rstImgPath.Length];

            for (int i = 0; i < teimage.Length; i++)
            {
                teimage[i] = teReadImageFromHD(ref rstImgPath[i]);
            }

            return teimage;
        }

        //释放图片
        public TeStatus teReleaseImages(TeImage[] stImage)
        {
            TeStatus stState = new TeStatus();

            for (int i = 0; i < stImage.Length; i++)
            {
                stState = teReleaseImage(ref stImage[i]);

                if (stState != 0)
                {
                    return stState;
                }
            }
            return 0;
        }

        //设置识别结果面积基于子图还是原图
        public void teSetRstAreaBackType(bool bMapbackFlag)
        {
            teSetRstAreaMapbackFlag(m_TreePredictHandle, bMapbackFlag);
        }

    }
}
