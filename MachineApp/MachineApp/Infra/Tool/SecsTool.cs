// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：SecsTool。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Business.Ports、Extension.Core.Concurrency、Extension.Core.Models、Extension.Imaging.Models、Extension.Automation.Device、Extension.Automation.Secs。导入不代表所有类型都会在运行时加载。
using MachineApp.Business.Ports;
using Extension.Core.Concurrency;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using MachineApp.Business.Order;
using SecsGem.Controls;
using SecsGem.Core;
using SecsGem.Runtime;
using Secs4Net;
using System;
using System.Threading.Tasks;
using WPFControls.Controls.Diagnostics;

namespace MachineApp.Infra.Tool
{
    /// <summary>
    /// SecsTool 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
    /// 继承或实现的契约：IProductionSecs。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SecsTool : IProductionSecs  
    {
        private readonly TryCatchHelper _errors;

        private readonly SecsGemRuntime _secsControl;
        private readonly SecsGem.Runtime.Variable.Data.ActiveReceive _activeReceive;
        private readonly ISecsGemRuntime _runtime;
        public ESecsMode CurrentMode => _runtime.GetState().Mode switch
        {
            SecsGemMode.OnlineLocal => ESecsMode.Online_Local,
            SecsGemMode.OnlineRemote => ESecsMode.Online_Remote,
            _ => ESecsMode.Offline
        };

        /// <summary>
        /// 初始化 SecsTool 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SecsTool(ISecsGemRuntime runtime, SecsGemRuntime secsControl, SecsGem.Runtime.Variable.Data.ActiveReceive activeReceive, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            _runtime = runtime;
            _secsControl = secsControl;
            _activeReceive = activeReceive;
        }
 
        /// <summary>
        /// 更新VID数据
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>

        public void Update( string pName, object pValue)
        {
           _errors.TryExecute(() =>
            {
                _runtime.SetVariable(pName, pValue);
            });
        }

        /// <summary>
        /// 追加VID数据
        /// </summary>
        /// <param name="pName"></param>
        /// <param name="pValue"></param>
        public void Append(string pName, object pValue)
        {
            _errors.TryExecute(() =>
            {
                _runtime.AppendVariable(pName, pValue);
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
            Func<Task> func = null;
            switch (CurrentMode)
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
 
        /// <summary>
        /// 发送S6F11
        /// </summary>
        /// <param name="pSEMIOrder">SEMIOrder</param>
        /// <returns></returns>
        public async Task SendS6F11(string pSEMIOrder)
        {
            try
            {
              
                if (!await _runtime.ReportEventAsync(pSEMIOrder))
                    throw new InvalidOperationException("S6F11 event report failed");
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
            await _secsControl.Send("S6F11", item, true);
        }

        /// <summary>
        /// 执行发送操作；对应入口为 SendRecipeReport。
        /// 正文中的关键协作调用包括 L、U2、A、SendS6F11；具体条件和先后次序见方法体。
        /// </summary>
        public Task SendRecipeReport(string recipeJson)
        {
            var body = Item.L(Item.U2(1), Item.U2(1), Item.L(Item.A(recipeJson)));
            return SendS6F11(body);
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
                var d3 = _activeReceive.GetandRemoveData("S2F41", pS2F42Order);
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

        /// <summary>
        /// 获取当前实现提供的值或对象；对应入口为 GetS2F41_Recipe。
        /// 正文中的关键协作调用包括 WaitForEventAsync、GetandRemoveData、IndexOf、Join；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// 此入口包含异常处理；失败时是上报、恢复还是继续抛出，请以 catch 分支为准。
        /// </summary>
        public async Task<string> GetS2F41_Recipe(string pLotID, string pS2F42Order)
        {
            try
            {
                //等待S2F41指令
                await EventHelper.WaitForEventAsync("S2F41");

                //获取数据
                var d3 = _activeReceive.GetandRemoveData("S2F41", pS2F42Order);

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
