using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace TeDemo
{

    //节点名信息集合
    [StructLayout(LayoutKind.Sequential)]
    public struct TeNodeNames
    {
        public int iNameNum; //节点名个数
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)TeArrLen.MAX_NODE_NUM)]
        public TeString[] pstNodeNames; //节点名集合
    };


    class teProjsAdmin
    {
#if DEBUG
        const string strTreePredictDll = "teTreePredictd.dll";
        const string strRuntimeDll = "teRuntimed.dll";
#else

        const string strTreePredictDll = "teTreePredict.dll";
        const string strRuntimeDll = "teRuntime.dll";
#endif

        [DllImport(strTreePredictDll, EntryPoint = "teGetModelsFromProjFile", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teGetModelsFromProjFile(ref TeString pstProjFilePath, [Out, MarshalAs(UnmanagedType.LPArray)]TeString[] pstModelsName, [Out, MarshalAs(UnmanagedType.LPArray)]int[] pstParentIdx, int iMaxLen, ref int piModelNum);

        [DllImport(strTreePredictDll, EntryPoint = "teGetSampleSizeFromProjFile", CallingConvention = CallingConvention.Cdecl)]
        static extern int teGetSampleSizeFromProjFile(ref TeString pstProjFilePath);

        [DllImport(strTreePredictDll, EntryPoint = "teInitMultiProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitMultiProj(ref IntPtr pnMultiProjsHandle, [Out, MarshalAs(UnmanagedType.LPArray)]IntPtr[] pnProjHandle, int iProjNum, TeString[] pstProjsPath, TeNodeNames[] pstNodesNames, TeSize stMaxSize, int iDevID, int iMaxObjNum);

        [DllImport(strTreePredictDll, EntryPoint = "teReleaseMultiProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseMultiProj(IntPtr nMultiProjsHandle);

        [DllImport(strTreePredictDll, EntryPoint = "teInitProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitProj(ref IntPtr pnProjHandle, ref TeString pstProjPath, TeString[] pstrResultNodeName, TeSize stMaxSize, int iResultNodeNum, int iDevID, int iMaxObjNum);

        [DllImport(strTreePredictDll, EntryPoint = "tePredictProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus tePredictProj(IntPtr nProjHandle, [In, MarshalAs(UnmanagedType.LPArray)]TeImage[] pSrcImage, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeNodeRst[] pstNodeRst, int iResultNodeNum, [In, MarshalAs(UnmanagedType.LPArray)]TePredictParam[] pstPredictParam = null, TeString[] pstrResultNodeName = null, bool bSort = false);

        [DllImport(strTreePredictDll, EntryPoint = "tePredictProjDyn", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus tePredictProjDyn(IntPtr nProjHandle, [In, MarshalAs(UnmanagedType.LPArray)]TeImage[] pSrcImage, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeNodeRstDyn[] pstNodeRst, int iResultNodeNum, [In, MarshalAs(UnmanagedType.LPArray)]TePredictParam[] pstPredictParam = null, TeString[] pstrResultNodeName = null, bool bSort = false);

        [DllImport(strTreePredictDll, EntryPoint = "teReleaseProj", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseProj(IntPtr nProjHandle);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstInSrcImageByFilling", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByFilling(ref TeImage pSrcImage, IntPtr pstNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstInSrcImageByRect", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstInSrcImageByRect(ref TeImage pSrcImage, IntPtr pstNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 20);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstDynInSrcImageByFilling", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstDynInSrcImageByFilling(ref TeImage pSrcImage, ref TeNodeRstDyn pstNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstDynInSrcImageByRect", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teDrawRstDynInSrcImageByRect(ref TeImage pSrcImage, ref TeNodeRstDyn pstNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 20);

        [DllImport(strTreePredictDll, EntryPoint = "teGetError", CallingConvention = CallingConvention.Cdecl)]
        static extern void teGetError(TeStatus eStatus, ref TeString pstrError);

        [DllImport(strRuntimeDll, EntryPoint = "teInitDll", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teInitDll(ref IntPtr nDllHandle);

        [DllImport(strRuntimeDll, EntryPoint = "teReleaseTeDll", CallingConvention = CallingConvention.Cdecl)]
        static extern int teReleaseTeDll(ref IntPtr nDllHandle);

        [DllImport(strTreePredictDll, EntryPoint = "teGetNodeParamInfo", CallingConvention = CallingConvention.Cdecl)]
        static extern bool teGetNodeParamInfo(ref TeString pstProjFilePath, ref TeString pstNodeName, ref TeNodeParamInfo pstDstNodeInfo);

        [DllImport(strTreePredictDll, EntryPoint = "teReadImageFromHD", CallingConvention = CallingConvention.Cdecl)]
        static extern TeImage teReadImageFromHD(ref TeString rstImgPath);

        [DllImport(strTreePredictDll, EntryPoint = "teReleaseImage", CallingConvention = CallingConvention.Cdecl)]
        static extern TeStatus teReleaseImage(ref TeImage rstImg);

        [DllImport(strTreePredictDll, EntryPoint = "teSetRstAreaMapbackFlag", CallingConvention = CallingConvention.Cdecl)]

        static extern void teSetRstAreaMapbackFlag(IntPtr nProjHandle, bool bMapbackFlag);


        [DllImport(strTreePredictDll, EntryPoint = "teGetMinBoundingRect", CallingConvention = CallingConvention.Cdecl)]

        static extern void teGetMinBoundingRect(TeSize stMaxSize, ref TeNodeRst stNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeRotatedRect[] pstNodeRotateRect);


        [DllImport(strTreePredictDll, EntryPoint = "teGetMinBoundingRectDyn", CallingConvention = CallingConvention.Cdecl)]

        static extern void teGetMinBoundingRectDyn(TeSize stMaxSize, ref TeNodeRstDyn stNodeRst, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeRotatedRect[] pstNodeRotateRect);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstInSrcImageByMinBoundingRect", CallingConvention = CallingConvention.Cdecl)]

        static extern void teDrawRstInSrcImageByMinBoundingRect(ref TeImage pSrcImage, IntPtr pstNodeRst, [In, MarshalAs(UnmanagedType.LPArray)]TeRotatedRect[] pstNodeRotateRect, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 20);

        [DllImport(strTreePredictDll, EntryPoint = "teDrawRstInSrcImageByMinBoundingRectDyn", CallingConvention = CallingConvention.Cdecl)]

        static extern void teDrawRstInSrcImageByMinBoundingRectDyn(ref TeImage pSrcImage, ref TeNodeRstDyn pstNodeRst, [In, MarshalAs(UnmanagedType.LPArray)]TeRotatedRect[] pstNodeRotateRect, [In, Out, MarshalAs(UnmanagedType.LPArray)]TeColor[] pstString2Color, float fLabelSize = 0.5f, int iThickness = 20);


        IntPtr m_pDllHandle;
        IntPtr m_nMultiProjHandle;
        int m_iProjNum = 0;
        List<TeString> m_projsPath = new List<TeString>();
        List<TeString> m_projsName = new List<TeString>();
        List<IntPtr> m_pnProjHandle = new List<IntPtr>();
        List<TeNodeNames> m_pstAllNodeNames = new List<TeNodeNames>();
        List<List<TeString>> m_pstInitNodeNames = new List<List<TeString>>();


        int m_iMaxObjNum = 0;
        int iRst = 0;

        public teProjsAdmin()
        {
            try
            {
                TeStatus teStatus = teInitDll(ref m_pDllHandle);
                if (teStatus == TeStatus.TE_COMPILER_NOT_ALLOWED_TO_RUN)
                {
                   throw new Exception("当前为release模式，请关闭visual studio，否则芯片可能会被锁死，tePrediction.dll 初始化失败");
                }
            }
            catch (System.Exception ex)
            {
                if (ex.Source != null)
                   throw new Exception( "节点树初始化失败，IOException message: {0}");
            }

        }


        //输入proj id返回proj 名
        public string teGetProjName(int iProjId)
        {
            return m_projsName[iProjId].pString.ToString();
        }



        //获取指定proj下需要初始化的节点个数
        public int teGetInitNodeNum(int iProjID)
        {
            //int iIdx = teFindProj(stProjName);
            if (iProjID <= -1)
            {
               throw new Exception("输入参数无效，请检查输入是否正确");
            }

            return m_pstInitNodeNames[iProjID].Count;
        }

        //添加一个proj, 返回值为该proj的id
        public int teAddProj(ref TeString pstProjPath, out TeString[] pstrNodeName, out int[] piParentIdx, out int iNodeNum, out int iSampleSize)
        {
            TeStatus eStatus = TeStatus.TE_SUCCESS;
            if (m_pDllHandle== IntPtr.Zero)
            {
                eStatus = teInitDll(ref m_pDllHandle);
            }
            if (eStatus != TeStatus.TE_SUCCESS)
            {
                TeString strErr = new TeString();
                teGetError(eStatus, ref strErr);
               throw new Exception("动态库加载失败！");                
            }

            pstrNodeName = new TeString[(int)TeArrLen.MAX_NODE_NUM];
            piParentIdx = new int[(int)TeArrLen.MAX_NODE_NUM];
            iNodeNum = 0;
            iSampleSize = 0;
            eStatus = teGetModelsFromProjFile(ref pstProjPath, pstrNodeName, piParentIdx, (int)TeArrLen.MAX_NODE_NUM, ref iNodeNum);

            if (eStatus != TeStatus.TE_SUCCESS)
            {
                TeString strErr = new TeString();
                teGetError(eStatus, ref strErr);
               throw new Exception("添加工程失败");
            }

            iSampleSize = teGetSampleSizeFromProjFile(ref pstProjPath);

            //记录proj信息
            m_iProjNum++;
            m_projsPath.Add(pstProjPath);
            m_projsName.Add(pstrNodeName[0]);

            //记录全部子节点
            TeNodeNames stNodeName = new TeNodeNames();
            stNodeName.pstNodeNames = new TeString[(int)TeArrLen.MAX_NODE_NUM];
            Array.Copy(pstrNodeName, 1, stNodeName.pstNodeNames, 0, iNodeNum - 1);
            stNodeName.iNameNum = iNodeNum - 1;
            m_pstAllNodeNames.Add(stNodeName);
            m_pstInitNodeNames.Add(new List<TeString>());

            return m_projsName.Count - 1;
        }

        //为指定proj添加一个待初始化节点
        public int teAddInitNode(int iProjID, TeString pstNodeName)
        {
            //检查是否为根节点
            if (m_projsName[iProjID].pString.ToString() == pstNodeName.pString.ToString())
            {
                //所有节点都设定为输出节点
                m_pstInitNodeNames[iProjID].Clear();
                for (int i = 0; i < m_pstAllNodeNames[iProjID].iNameNum; i++)
                {
                    m_pstInitNodeNames[iProjID].Add(m_pstAllNodeNames[iProjID].pstNodeNames[i]);
                }
                return 1;
            }

            //检查是否已存在该节点
            for (int j = 0; j < m_pstInitNodeNames[iProjID].Count; j++)
            {
                if (m_pstInitNodeNames[iProjID][j].pString.ToString() == pstNodeName.pString.ToString())
                {
                    return -1;
                }
            }

            m_pstInitNodeNames[iProjID].Add(pstNodeName);

            return 0;

        }

        //移除指定proj下的指定待初始化节点
        public int teRemoveInitNode(int iProjID, TeString pstNodeName)
        {
            //检查是否为根节点
            if (m_projsName[iProjID].pString.ToString() == pstNodeName.pString.ToString())
            {
                //所有节点都设定为不输出节点
                m_pstInitNodeNames[iProjID].Clear();
                return 1;
            }

            //检查是否存在该节点
            for (int j = 0; j < m_pstInitNodeNames[iProjID].Count; j++)
            {
                if (m_pstInitNodeNames[iProjID][j].pString.ToString() == pstNodeName.pString.ToString())
                {
                    m_pstInitNodeNames[iProjID].RemoveAt(j);

                    return 0;
                }
            }

            return -1;
        }

        //初始化所有proj
        public int teInitProjs(int iMaxWidth, int iMaxHeight, int iDevID, int iMaxObjNum)
        {
            m_iMaxObjNum = iMaxObjNum;
            TeNodeNames[] pstNodesNames = new TeNodeNames[m_iProjNum];

            for (int i = 0; i < m_iProjNum; i++)
            {
                if (m_pstInitNodeNames[i].Count == 0)
                {
                   throw new Exception(string.Format("当前未设置工程 : {0} \n (选定节点右键设定)"+m_projsName[i].pString)+"当前有工程未设定要预测的节点");
                }
                pstNodesNames[i].iNameNum = m_pstInitNodeNames[i].Count;
                pstNodesNames[i].pstNodeNames = new TeString[(int)TeArrLen.MAX_NODE_NUM];
                Array.Copy(m_pstInitNodeNames[i].ToArray(), pstNodesNames[i].pstNodeNames, pstNodesNames[i].iNameNum);
            }
            iRst = 0;
            try
            {
                IntPtr[] pnProjHandle = new IntPtr[m_iProjNum];
                TeStatus eStatus = teInitMultiProj(ref m_nMultiProjHandle, pnProjHandle, m_iProjNum, m_projsPath.ToArray(), pstNodesNames, new TeSize(iMaxWidth, iMaxHeight), iDevID, iMaxObjNum);
                if (eStatus != TeStatus.TE_SUCCESS)
                {
                    TeString strError = new TeString();
                    teGetError(eStatus, ref strError);
                   throw new Exception("项目初始化失败");
                }
                else
                {
                    m_pnProjHandle = pnProjHandle.ToList();
                }
            }
            catch (System.Exception ex)
            {
                if (ex.Source != null)
                   throw new Exception("节点树初始化失败，IOException message: {0}");
                throw;
            }

            return iRst;
        }


        public double tePredictDyn(int iProjID, ref TeImage[] pSrcImage, ref TeNodeRstDyn[] pstNodeRstSet, ref TePredictParam[] pstParam)
        {
            TeStatus eStatus = TeStatus.TE_SUCCESS;

            if (iProjID > -1)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                eStatus = tePredictProjDyn(m_pnProjHandle[iProjID], pSrcImage, pstNodeRstSet, m_pstInitNodeNames[iProjID].Count, pstParam, m_pstInitNodeNames[iProjID].ToArray(), true);
                sw.Stop();
                TimeSpan ts2 = sw.Elapsed;

                if (eStatus != TeStatus.TE_SUCCESS)
                {
                    TeString strError = new TeString();
                    teGetError(eStatus, ref strError);
                   throw new Exception( "项目预测失败");
                }

                return ts2.TotalMilliseconds;
            }

           throw new Exception("请检查输入是否正确");
        }


        public double tePredict(int iProjID, ref TeImage[] pSrcImage, ref TeNodeRst[] pstNodeRstSet, ref TePredictParam[] pstParam)
        {
            TeStatus eStatus = TeStatus.TE_SUCCESS;

            //int iIdx = teFindProj(strProjName);
            if (iProjID > -1)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                eStatus = tePredictProj(m_pnProjHandle[iProjID], pSrcImage, pstNodeRstSet, m_pstInitNodeNames[iProjID].Count, pstParam, m_pstInitNodeNames[iProjID].ToArray());
                sw.Stop();
                TimeSpan ts2 = sw.Elapsed;

                if (eStatus != TeStatus.TE_SUCCESS)
                {
                    TeString strError = new TeString();
                    teGetError(eStatus, ref strError);
                   throw new Exception("项目预测失败");
                }

                return ts2.TotalMilliseconds;
            }

           throw new Exception("请检查输入是否正确");
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

        //对指定节点的预测结果作图
        public int teShowRstDynInSrcImage(ref TeImage pSrcImage, ref TeNodeRstDyn pstNodeRst, TeColor[] pstString2Color = null, float fLabelSize = 0.5f, int iThickness = 2)
        {


            if (TeToolType.E_PixelDetect_Tool == pstNodeRst.eNodeType && IntPtr.Zero != pstNodeRst.pnLabelMat)
            {
                //以填充形式将结果在原图上画出
                teDrawRstDynInSrcImageByFilling(ref pSrcImage, ref pstNodeRst, pstString2Color);
            }
            //以矩形框形式将结果在原图上画出
            teDrawRstDynInSrcImageByRect(ref pSrcImage, ref pstNodeRst, pstString2Color, fLabelSize, iThickness);

            return 0;
        }

        public int teReleaseProjs()
        {
            m_iProjNum = 0;
            m_projsPath.Clear();
            m_pnProjHandle.Clear();
            m_projsName.Clear();
            m_pstAllNodeNames.Clear();
            m_pstInitNodeNames.Clear();

            if (m_nMultiProjHandle != IntPtr.Zero)
            {
                TeStatus eStatus = teReleaseMultiProj(m_nMultiProjHandle);
                m_nMultiProjHandle = IntPtr.Zero;
            }

            if (m_pDllHandle != IntPtr.Zero)
            {
                teReleaseTeDll(ref m_pDllHandle);
            }

            return 0;
        }

        //结果转换
        public void teGetObjects(out TeObjectInSrc[] pstObjecInSrc, ref TeNodeRstDyn pstNodeRst)
        {
            pstObjecInSrc = new TeObjectInSrc[pstNodeRst.iObjNum];

            for (int i = 0; i < pstNodeRst.iObjNum; i++)
            {
                IntPtr pPonitor = new IntPtr(pstNodeRst.pstObjectInfo.ToInt64() + Marshal.SizeOf(typeof(TeObjectInSrc)) * i);

                pstObjecInSrc[i] = (TeObjectInSrc)Marshal.PtrToStructure(pPonitor, typeof(TeObjectInSrc));
            }
        }

        //获取工程各个节点的信息
        public TeNodeParamInfo teGetNodeParamInfo(ref TeString pstProjFilePath, ref TeString pstNodeName)
        {
            TeNodeParamInfo pstDstNodeInfo = new TeNodeParamInfo();

            teGetNodeParamInfo(ref pstProjFilePath, ref pstNodeName, ref pstDstNodeInfo);

            return pstDstNodeInfo;
        }
        public TeImage[] teReadImages(TeString[] rstImgPath)
        {
            TeImage[] teimage = new TeImage[rstImgPath.Length];

            for (int i = 0; i < teimage.Length; i++)
            {
                teimage[i] = teReadImageFromHD(ref rstImgPath[i]);
            }

            return teimage;
        }

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
        public void teSetRstAreaBackType(int iProjID, bool bMapbackFlag)
        {
            teSetRstAreaMapbackFlag(m_pnProjHandle[iProjID], bMapbackFlag);
        }

        public void teGetGTtypesFromProjF(int iProjID, TeString pstrResultNodeName,out byte[] pcLabellist)
        {

            TeString strprojpPath= m_projsPath[iProjID];

            TeNodeParamInfo pstDstNodeInfo = new TeNodeParamInfo();

            pstDstNodeInfo=teGetNodeParamInfo(ref strprojpPath,ref pstrResultNodeName);

            pcLabellist = pstDstNodeInfo.pcLabelList;

        }

        public TeRotatedRect[] teGetNodeRstMinAreaRectDyn(int iNodeLabelMat_Width,int iNodeLabelMat_Height,TeNodeRstDyn pstNodeRstDyn)
        {
            TeRotatedRect[] teRotatedRects = new TeRotatedRect[pstNodeRstDyn.iObjNum];
          
            teGetMinBoundingRectDyn(new TeSize(iNodeLabelMat_Width, iNodeLabelMat_Height),ref pstNodeRstDyn, teRotatedRects);

            return teRotatedRects;
        }

        public TeRotatedRect[] teGetNodeRstMinAreaRect(int iNodeLabelMat_Width, int iNodeLabelMat_Height, TeNodeRst pstNodeRstDyn)
        {
            TeRotatedRect[] teRotatedRects = new TeRotatedRect[pstNodeRstDyn.iObjNum];

            teGetMinBoundingRect(new TeSize(iNodeLabelMat_Width, iNodeLabelMat_Height), ref pstNodeRstDyn, teRotatedRects);

            return teRotatedRects;
        }

        public int teDrawRstInSrcImageByMinAreaRcet(ref TeImage pSrcImage, ref TeNodeRst pstNodeRst,TeRotatedRect[] teRotatedRects, TeColor[] pstString2Color = null, float fLabelSize = 0.5f, int iThickness = 2)
        {      
            if (TeToolType.E_PixelDetect_Tool == pstNodeRst.eNodeType && IntPtr.Zero != pstNodeRst.pnLabelMat)
            {
                int iSize = Marshal.SizeOf(typeof(TeNodeRst));
                IntPtr intPtr = Marshal.AllocHGlobal(iSize);
                Marshal.StructureToPtr(pstNodeRst, intPtr, true);

                //以填充形式将结果在原图上画出
                teDrawRstInSrcImageByMinBoundingRect(ref pSrcImage, intPtr, teRotatedRects, pstString2Color, fLabelSize, iThickness);

                Marshal.FreeHGlobal(intPtr);
            }          
            return 0;
        }

        public int teDrawRstDynInSrcImageByMinAreaRcet(ref TeImage pSrcImage, ref TeNodeRstDyn pstNodeRst, TeRotatedRect[] teRotatedRects, TeColor[] pstString2Color = null, float fLabelSize = 0.5f, int iThickness = 2)
        {
            if (TeToolType.E_PixelDetect_Tool == pstNodeRst.eNodeType && IntPtr.Zero != pstNodeRst.pnLabelMat)
            {
                //以填充形式将结果在原图上画出
                teDrawRstInSrcImageByMinBoundingRectDyn(ref pSrcImage,ref pstNodeRst, teRotatedRects, pstString2Color, fLabelSize, iThickness);
            }
            return 0;
        }
    }
}
