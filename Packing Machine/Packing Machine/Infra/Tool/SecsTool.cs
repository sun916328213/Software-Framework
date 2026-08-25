using Extension.Helper.TaskHelper;
using Extension.Struct;
using Packing_Machine.Business.Order;
using Secs;
using Secs.Order.S6;
using Secs4Net;
using System;
using System.Threading.Tasks;
using WPFControl.Helper;

namespace Packing_Machine.Infra.Tool
{
    public class SecsTool  
    {
        internal SecsControl _SecsControl;

        public SecsTool(SecsControl secsControl)
        {
            _SecsControl= secsControl;
        }
 
        /// <summary>
        /// 更新VID数据
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>

        public void Update( string pName, object pValue)
        {
           TryCatchHelper.TryExecute(() =>
            {
                _SecsControl._VIDManager.UpdateVID(pName, pValue);
            });
        }

        /// <summary>
        /// 追加VID数据
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        public void Append(string pName, object pValue)
        {
            TryCatchHelper.TryExecute(() =>
            {
                _SecsControl._VIDManager.AppendVID(pName, pValue);
            });
        }


        /// <summary>
        /// 执行程序
        /// </summary>
        /// <param name="action_Offline">离线程序</param>
        /// <param name="action_Online_Local">在线本地程序</param>
        /// <param name="action_Online_Remote">在线远程程序</param>
        public async Task Run(Func<Task> action_Offline, Func<Task> action_Online_Local, Func<Task> action_Online_Remote)
        {
            try
            {

                Func<Task> func = null;

                switch (_SecsControl.GetDeviceMode())
                {
                    case ESecsMode.Offline:
                        func = action_Offline;
                        break;
                    case ESecsMode.Online_Local:
                        func = action_Online_Local;
                        break;
                    case ESecsMode.Online_Remote:
                        func = action_Online_Remote;
                        break;
                }
                if (func != null)
                    await func();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
 
        /// <summary>
        /// 发送S6F11
        /// </summary>
        /// <param name="pSEMIOrder">SEMIOrder</param>
        /// <returns></returns>
        public async Task SendS6F11(string pSEMIOrder)
        {
            try
            {
              
                var data = _SecsControl._VIDManager.CEID[pSEMIOrder];
                //告诉EAP数据失败
                await _SecsControl.SendItem("S6F11", true, new S6F11Info() { CEID = data });
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                throw new Exception(pSEMIOrder + ":" + message);
            }
        }

        /// <summary>
        /// 发送S6F11
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task SendS6F11(Item item)
        {
            await _SecsControl.Send("S6F11", item, true);
        }




        /// <summary>
        /// 获取S2F41数据 判断传入ID与EAP反馈ID是否一致
        /// </summary>
        /// <param name="pLotID">LOTID</param>
        /// <param name="pS2F42Order">S2F42Order</param>

        public async Task<bool> GetS2F41(string pLotID, string pS2F42Order)
        {
            try
            {
                //等待S2F41指令
                await EventHelper.WaitForEventAsync("S2F41");

                //获取数据
                var d3 = _SecsControl._VIDManager.ActiveReceive.GetandRemoveData("S2F41", pS2F42Order);
                //告诉EAP数据成功
                int i1 = d3.CommandParam.IndexOf(S2F42Order.Check_Result) + 1;
                int i2 = d3.CommandParam.IndexOf(S2F42Order.FOSBID) + 1;
                string CheckData = i1 > 0 ? d3.CommandParam[i1] : null;
                string RFIDInfo_EAP = i2 > 0 ? d3.CommandParam[i2] : null;

                if (RFIDInfo_EAP == pLotID && CheckData == "1")
                {
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                throw new Exception(pS2F42Order + ":" + message);
            }
        }

        public async Task<string> GetS2F41_Recipe(string pLotID, string pS2F42Order)
        {
            try
            {
                //等待S2F41指令
                await EventHelper.WaitForEventAsync("S2F41");

                //获取数据
                var d3 = _SecsControl._VIDManager.ActiveReceive.GetandRemoveData("S2F41", pS2F42Order);

                int i1 = d3.CommandParam.IndexOf(S2F42Order.Recipe_ID) + 1;
                int i2 = d3.CommandParam.IndexOf(S2F42Order.FOSBID) + 1;
                string RecipeID = i1 > 0 ? d3.CommandParam[i1] : null;
                string RFIDInfo_EAP = i2 > 0 ? d3.CommandParam[i2] : null;


                if (RFIDInfo_EAP == pLotID)
                {
                    return RecipeID;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                throw new Exception(pS2F42Order + ":" + message);
            }
        }
    }
}
