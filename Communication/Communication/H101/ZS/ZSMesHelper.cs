using Extension.Struct;
using Miracom.CliFrx;
using Miracom.MESCore;
using Miracom.MESCore.Controls;
using Miracom.TRSCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.H101.ZS
{
    /// <summary>
    /// 获取信息输入信息结构体
    /// </summary>
    public struct SDataInfo_Node
    {
        public string PASSPORT { get; set; }

        public char LANGUAGE { get; set; }
        public string USERID { get; set; }
        public string PASSWORD { get; set; }
        public string FACTORY { get; set; }
        public char PROCSTEP { get; set; }
        /// <summary>
        /// FOSBID
        /// </summary>
        public string CRR_ID { get; set; }
        public string RES_ID { get; set; }
    }

    /// <summary>
    /// 获取标签输入信息结构体
    /// </summary>
    public struct SLabelInfo_Node
    {
        public string FACTORY { get; set; }
        public char LANGUAGE { get; set; }
        public string PASSWORD { get; set; }
        public string USERID { get; set; }
        public char PROCSTEP { get; set; }
        public string SCREEN_ID { get; set; }
        /// <summary>
        /// 文件创建时间
        /// </summary>
        public string CREATION_TIME { get; set; }
        /// <summary>
        /// 文件大小
        /// </summary>
        public int FILESIZE { get; set; }
    }

    /// <summary>
    /// 打印模式
    /// </summary>
    public enum EPrintModel
    {
        Spread,
        BarTender,
    }

    /// <summary>
    /// <summary>
    /// 标签信息结构体
    /// </summary>
    /// </summary>
    public struct SLabelInfo
    {
        /// <summary>
        /// 标签名称
        /// </summary>
        public string LabelName { get; set; }
        /// <summary>
        /// 标签位置信息
        /// </summary>
        public string LabelPosInfo { get; set; }
        /// <summary>
        /// 打印模式
        /// </summary>
        public EPrintModel PrintModel { get; set; }
        /// <summary>
        /// 标签信息
        /// </summary>
        public List<KeyValue> LabelInfo { get; set; }
    }

    /// <summary>
    /// 输出数据结构
    /// </summary>
    public struct SOutData
    {
        /// <summary>
        /// TRSNode格式数据
        /// </summary>
        public TRSNode TRSNode { get; set; }
        /// <summary>
        /// 数据集合
        /// </summary>
        public List<KeyValue> Data { get; set; }
    }
    public class ZSMesHelper:H101Helper
    {
        List<SLabelInfo> LabelInfos = new List<SLabelInfo>();

        /// <summary>
        /// 获取信息 所有信息
        /// </summary>
        /// <param name="pModuleName"></param>
        /// <param name="pServiceName"></param>
        /// <param name="pDataInfo"></param>
        public bool GetInfo(out SOutData pInfos, string pModuleName, string pServiceName, SDataInfo_Node pDataInfo)
        {
            pInfos = new SOutData();
            try
            {
                TRSNode OutNode;
                var ret = CallService<SDataInfo_Node>(out OutNode, pModuleName, pServiceName, pDataInfo);


                pInfos.TRSNode = OutNode;
                pInfos.Data = null;
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 从所有信息中获取标签信息
        /// </summary>
        /// <param name="pLabelInfos"></param>
        /// <param name="pInfos"></param>
        /// <returns></returns>
        public bool GetLabelInfo(out SLabelInfo pLabelInfos, TRSNode pTRSNode)
        {
            pLabelInfos = new SLabelInfo();
            try
            {
                //获取标签信息
                var screen = pTRSNode.GetList("LABEL_LIST");

                List<KeyValue> data = new List<KeyValue>();
                AnalysisData(ref data, screen);


                pLabelInfos.LabelInfo = data;
                return   true ;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 获取标签模版
        /// </summary>
        /// <param name="pLabelInfos">输出标签信息</param>
        /// <param name="pModuleName">ModuleName</param>
        /// <param name="pServiceName">ServiceName</param>
        /// <param name="pDataInfo">输入信息</param>
        /// <param name="pFile">更新Label模版的位置</param>
        /// <returns></returns>
        public  SLabelInfo GetLabelModel(string pModuleName, string pServiceName, SLabelInfo_Node pDataInfo, string pFile)
        {
            try
            {
                SLabelInfo pLabelInfos = new SLabelInfo();
                pLabelInfos.LabelName = pDataInfo.SCREEN_ID;
                string pLabelName = pDataInfo.SCREEN_ID;
                TRSNode OutNode;
                //判断文件是否需要更新
                string modifytime = GetModifyTime(pFile, pLabelName);
                pDataInfo.CREATION_TIME = modifytime;//赋值文件的修改日期时间
                var ret = CallService<SLabelInfo_Node>(out OutNode, pModuleName, pServiceName, pDataInfo);
                //更新文件
                if (OutNode.GetChar("NEED_UPDATE") == 'Y')
                    UpdateLabelModel(OutNode, pFile, pLabelName);
                //更新打印方式
                if (OutNode.GetChar("39CODE_FLAG") == 'Y')
                {
                    pLabelInfos.PrintModel = EPrintModel.BarTender;
                }
                else
                {
                    pLabelInfos.PrintModel = EPrintModel.Spread;
                }
                return   pLabelInfos ;
            }
            catch (Exception ex)
            {
               throw ex;
            }
        }

        /// <summary>
        /// 返回文件修改日期 不存在返回较早的时间
        /// </summary>
        /// <param name="pLabelFile">文件路径</param>
        /// <param name="pLabeName">文件名称</param>
        /// <returns></returns>
        private string GetModifyTime(string pLabelFile, string pLabeName)
        {
            string path = Path.Combine(pLabelFile, pLabeName + ".gzip");
            if (File.Exists(path))
                return File.GetCreationTime(path).ToString("yyyyMMddHHmmss");
            else
                return "19991231000000";
        }

        /// <summary>
        /// 更新XMl文件
        /// </summary>
        /// <param name="pOutNode">TRSNode数据</param>
        /// <param name="pLabelFile">文件路径</param>
        /// <param name="pLabeName">文件名称</param>
        /// <returns></returns>
        private bool UpdateLabelModel(TRSNode pOutNode, string pLabelFile, string pLabeName)
        {
            try
            {
                string sPath = Path.Combine(pLabelFile, pLabeName);
                FileStream fs = File.Open(sPath + ".gzip", FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);
                byte[] buffer;

                string A = TRSConvertorDefine.Convertor.ToXmlString(pOutNode);
                fs.Flush();
                buffer = pOutNode.GetBlob(MPGC.MP_BIN_DATA_2);
                bw.Write(buffer);
                bw.Close();
                fs.Close();
                DateTime createtime = MPCF.ToDate(pOutNode.GetString("CREATION TIME"));
                File.SetCreationTime(sPath + ".gzip", createtime);
                MPCR.ZipDecompress(sPath);
                return    true  ;

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        //private void Print(string Lotid,string printname)
        //{
        //    udcFlexibleScreen udcScreen = new udcFlexibleScreen();
        //    TRSNode inScreen_node = new TRSNode();
        //    TRSNode outScreen_node = new TRSNode();
        //    udcScreen.InitFlexibleScreen();
        //    udcScreen.ScreenSpread.Tag = "Change Cell";
        //    udcScreen.ProcStep = '1';
        //    udcScreen.LotID = Lotid;
        //    udcScreen.ScreenID = "";
        //    MPCR.SetInMsg(inScreen_node);
        //    inScreen_node.ProcStep = '1';
        //    inScreen_node.AddString("LoT_ID", uteLotID.Text);
        //    //FOR INGOT
        //    inScreen_node.AddString("MAT_ID", LOT.GetString("MAT_ID"));
        //    inScreen_node.AddInt("MAI_VER", LOI.GetInt("MAT_VER"));
        //    inScreen_node.AddString("ORG_LOT_ID", uteLotID.Text);

        //    if (outMFOOption.GetString("DATA_2") != "")
        //    {
        //        if (udcScreen.LoadDesign(outMFOOption.GetString("DATA_2")) == false) return;
        //        if (udcScreen.SetServiceData(inScreen_node, ref outScreen_node) == false) return;
        //        udcScreen.ScreenSpread.ActiveSheet.PrintInfo.Printer=MPCF.Trim(printname);
        //        udcScreen.ScreenSpread.PrintSheet(0);
        //    }

        //}
    }
}
