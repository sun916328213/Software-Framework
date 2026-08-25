using Extension.Struct;
using Miracom.CliFrx;
using Miracom.MESCore;
using Miracom.MsgHandler;
using Miracom.TRSCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace Communication.H101
{
    public class H101Helper
    {
        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="pIPAdress">IP地址</param>
        /// <param name="pChannel"></param>
        /// <param name="SiteId"></param>
        /// <param name="pTimeOut"></param>
        /// <returns></returns>
        public bool Connect(string DeviceName, string pIPAdress, string SiteId = "MPT1", string Module = "ZSM", int pTimeOut = 2000)
        {
            try
            {
                MPIF.gInit = new clsInitialFunctionImp();
                MPGV.gsProgramID = DeviceName;
                MPIF.gInit.LoadResource();
                MPGV.giRequestReplyWaitTime = pTimeOut;
                MPGV.gsSiteID = SiteId;
                MPGV.gsRemoteAddress = pIPAdress;
                MPGV.gsSiteNickName = SiteId;

                if (MPIF.gInit.InitMsgHandler() == false)
                {
                    return false;
                }

                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            try
            {
                MPIF.gInit.TermMsgHandler();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 解析数据
        /// </summary>
        /// <param name="pNodeData"></param>
        /// <param name="pKeys"></param>
        /// <returns></returns>
        public bool AnalysisData(ref List<KeyValue> pResultData, TRSNode pNodeData)
        {
            try
            {
                if (pNodeData.MemberCount > 0)
                {
                    foreach (TRSNode item in pNodeData.Members)
                    {
                        KeyValue sValue = new KeyValue();
                        sValue.Key = item.Name;
                        sValue.Value = item.Value;
                        pResultData.Add(sValue);
                    }
                }
                if (pNodeData.ListCount > 0)
                {
                    foreach (var list in pNodeData.Lists)
                    {
                        List<TRSNode> nodes = list.items;
                        for (int index = 0; index < nodes.Count; index++)
                        {
                            TRSNode inode = nodes[index];
                            AnalysisData(ref pResultData, inode);
                        }
                    }
                }
                return  true;
            }
            catch (Exception ex)
            {
                string str = ex.Message + ex.StackTrace;
                return  false;
            }
        }

        /// <summary>
        /// 解析数据
        /// </summary>
        /// <param name="pResultData"></param>
        /// <param name="pNodeData"></param>
        /// <returns></returns>
        public bool AnalysisData(ref List<KeyValue> pResultData, List<TRSNode> pNodeData)
        {
            try
            {
                for (int index = 0; index < pNodeData.Count; index++)
                {
                    TRSNode inode = pNodeData[index];
                    AnalysisData(ref pResultData, inode);
                }
                return true;
            }
            catch (Exception ex)
            {
                string str = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pTRSNode"></param>
        /// <param name="pDataInfo"></param>
        internal void Add<T>(ref TRSNode pTRSNode, T pDataInfo)
        {
            Type type = pDataInfo.GetType();
            foreach (System.Reflection.PropertyInfo item in type.GetProperties())
            {
                string name = item.Name;
                object value = item.GetValue(pDataInfo);
                string propertyType = item.PropertyType.Name.ToLower();

                switch (propertyType)
                {
                    case "string":
                        pTRSNode.AddString(name, value.ToString());
                        break;
                    case "char":
                        pTRSNode.AddChar(name, (char)value);
                        break;
                    case "int":
                        pTRSNode.AddInt(name, (int)value);
                        break;
                    case "double":
                        pTRSNode.AddDouble(name, (double)value);
                        break;
                }
            }
        }

        /// <summary>
        /// 呼叫H101服务器
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pOutNode">TRSNode数据</param>
        /// <param name="pModuleName"></param>
        /// <param name="pServiceName"></param>
        /// <param name="pDataInfo"></param>
        /// <returns></returns>
        internal bool CallService<T>(out TRSNode pOutNode, string pModuleName, string pServiceName, T pDataInfo)
        {
            pOutNode = null;
            try
            {
                TRSNode InNode = new TRSNode(pServiceName + "_IN");
                TRSNode OutNode = new TRSNode(pServiceName + "_OUT");

                Add<T>(ref InNode, pDataInfo);
                try
                {
                    if (MPCR.CallService(pModuleName, pServiceName, InNode, ref OutNode, true) == false)
                    {
                        string ErrCode = OutNode.GetString("MSGCODE");
                        string ErrMessage = OutNode.GetString("MSG");

                        return false;
                    }
                }
                catch { Exception ex; }
                pOutNode = OutNode;
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 保存文件数据
        /// </summary>
        /// <param name="pPath">文件路径</param>
        /// <param name="pFileName">文件名称</param>
        /// <param name="pTRSNode">数据</param>
        public void SaveData(string pPath, string pFileName, TRSNode pTRSNode)
        {
            try
            {
                string a = Miracom.TRSCore.TRSConvertorDefine.Convertor.ToXmlString(pTRSNode);
                if (!Directory.Exists(pPath))
                    Directory.CreateDirectory(pPath);
                string file = Path.Combine(pPath, pFileName);
                System.IO.File.WriteAllText(file, a);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// 保存文件数据
        /// </summary>
        /// <param name="path">保存路径</param>
        /// <param name="tRSNode">数据</param>
        public void SaveData(string path,TRSNode tRSNode)
        {
            try
            {
                string a = Miracom.TRSCore.TRSConvertorDefine.Convertor.ToXmlString(tRSNode);
                System.IO.File.WriteAllText(path, a);
            }
            catch (Exception ex)
            {
               throw ex;
            }
        }

        /// <summary>
        /// 匹配信息
        /// </summary>
        /// <param name="pInfos">List<SValue> 完整数据</param>
        /// <param name="pValue">匹配值</param>
        /// <returns>所有匹配数据的Index</returns>
        internal List<int> Match(List<KeyValue> pInfos, string pValue)
        {
            List<int> ret = pInfos
                .Select((item, index) => new { item, index })
                .Where(s => s.item.Value == pValue).ToList()
                .Select(s => s.index).ToList();

            return ret;
        }
    }
}
