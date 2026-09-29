using Extension.Core.Collections;
using Extension.UI.Controls;
using Extension.Core.Models;
using Extension.Imaging.Models;
using Extension.Automation.Device;
using Extension.Automation.Secs;
using DataManager.Log.Definition;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.ProgramControl.Models;


namespace Packing_Machine.Business.Station
{
	public class LabelInfo:Result
	{
		/// <summary>
		/// 标签信息集合
		/// </summary>
		public List<string> LabelInfos { get; set; }=new List<string>();

		/// <summary>
		/// 标签信息复拍集合
		/// </summary>
		public List<string> LabelInfo_Check { get; set; } = new List<string>();
        /// <summary>
        /// 当前读取标签的信息
        /// </summary>
        internal string CurLabelInfo { get; set; }
	 
	}

    public enum ELabel
    { 
        打印标签,
        FOSB贴前检测,
        FOSB贴后检测,
        AL贴前检测,
        AL贴后检测,
    }

	public class  Label : StationBase<LabelInfo>
	{

		LabelInfo curInfo = new LabelInfo();

        internal string StationName = " ";

        int PrintCount = -1;

		public Label(IContainerProvider container) : base(container)
		{
		}
 
        /// <summary>
        /// Sensor触发
        /// </summary>
        public async Task Trigger(EProcess process, ELabel order,string pLCOrder)
        {
            string ID="", Step="";
            try
            {
                StationName = process + "_" + order.ToString() + ": ";
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID );
                Step = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.Function);
                // 先把标签序号取出来：下面 Step 会被拼成 "标签N_" 供视觉/日志使用，
                // 原写法之后又对 Step 做 Convert.ToInt32，必然 FormatException，被外层 catch
                // 吞成 SysError，表现为「打印标签」流程永远走不到上报那一步。
                int labelIndex = int.Parse(Step) % 10 + 1;
                Step = "标签" + labelIndex + "_";
                switch (order)
                {
                    case ELabel.打印标签:
                        //根据配方获取标签名称
                        string labeltype = "";
                        // 这里上报的是「第几个标签」的序号，不是标签位置的 ID。
                        // 若要按 LabelPosition 配的 ID 上报，得先打通配方（CRecipe）到本流程的桥。
                        await Print(ID, labelIndex, labeltype);
                        curInfo.CheckResult = EPLCResult.OK;
                        break;
                    case ELabel.FOSB贴前检测:
                        await Read(Step, ID,GlobalData.VisionItem_LabelPrint);
                        break;
                    case ELabel.FOSB贴后检测:
                        await FitRead(Step, ID, GlobalData.VisionItem_Label_FOSB);
                        break;
                    case ELabel.AL贴前检测:
                        await Read(Step, ID, GlobalData.VisionItem_LabelPrint);
                        break;
                    case ELabel.AL贴后检测:
                        await FitRead(Step, ID, GlobalData.VisionItem_Label_AL);
                        break;
                }
            }
            catch (Exception ex)
            {
                curInfo.CheckResult = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + message, ELogLevel.SysError);
            }
            finally
            {
                try
                {   //通知PLC结果
                    _PLCMethod.SendResult(StationName, pLCOrder, curInfo.CheckResult);
                    switch (order)
                    {
                        case ELabel.打印标签:
                         
                            break;
                        case ELabel.FOSB贴前检测:
                        case ELabel.AL贴前检测:
                        case ELabel.FOSB贴后检测:
                        case ELabel.AL贴后检测:
                            //将结果添加到集合中
                            //Infos[Infos.Count - 1].LabelInfo_Check.Add(curInfo.CurLabelInfo);
                            //Infos[Infos.Count - 1].CheckResult = curInfo.CheckResult;
                            AddUpdate(ID, curInfo);
                            break;
                    }
                    curInfo = new LabelInfo();//复位
                }
                catch (Exception ex)
                {

                }
            }
        }

        /// <summary>
        /// 打印标签流程
        /// </summary>
        private async Task  Print(string LotID, int labelindex,string labeltype)
        {
            var mode = _SecsMethod.CurrentMode;
            _LogInfoService.AddRunInfo(StationName + Message.打印标签.GetMessage());
            switch (mode)
            {
                case ESecsMode.Offline:
                case ESecsMode.Online_Local:
                    //本地打印标签
                    // await _PrintManagerService.Print(labelindex);
                    await _PrintManagerService.PrintLabel_Path("C:\\Users\\CTOS\\Documents\\文档1.btw", "ZDesigner ZT610-600dpi ZPL");
                    break;
                case ESecsMode.Online_Remote:
                    //更新VID
                    _SecsMethod.Update(VIDOrder.FOSBID, LotID);
                    _SecsMethod.Update(VIDOrder.LabelType, labeltype);          //标签名称
                    _SecsMethod.Update(VIDOrder.LabelPosition, labelindex);//标签位置
                    //通知SEMI打印标签
                    await _SecsMethod.SendS6F11(EventOrder.Label_Print);
                    break;
            }
        }

 
        /// <summary>
        /// 贴前检测
        /// </summary>
        /// <param name="LotID"></param>
        private async Task Read(string index,string LotID ,VisionInspectionItem visionitem)
        {
            Func<Task> action = new Func<Task>( async() =>
            {
                curInfo = ReadLabel(index,LotID, visionitem);
            });

            Func<Task >  action1 = new Func<Task>(async () =>
            {
                try
                {
                    curInfo = ReadLabel(index,LotID,GlobalData.VisionItem_LabelPrint);

                    //刷新VID信息-----------------------------------------
                    _SecsMethod.Update(VIDOrder.FOSBID, LotID);
                    _SecsMethod.Update(VIDOrder.LabelInfos, curInfo.LabelInfos);  //标签信息

                    //将数据写入EAP
                    await _SecsMethod.SendS6F11(EventOrder.LabelInfo);

                    //等待S2F41指令
                    var d4 = await _SecsMethod.GetS2F41(LotID, S2F42Order.LabelInfo_Check);
                    if (!d4)
                    {
                        curInfo.CheckResult = EPLCResult.NG;
                        await _SecsMethod.SendS6F11(EventOrder.LabelInfo_Check_Fail);
                        _LogInfoService.AddRunInfo(StationName + Message.EAP回复校验失败.GetMessage(), ELogLevel.Error);
                    }
                    else
                    {
                        curInfo.CheckResult = EPLCResult.OK;
                        await _SecsMethod.SendS6F11(EventOrder.LabelInfo_Check_Success);
                        _LogInfoService.AddRunInfo(StationName + Message.EAP回复校验成功.GetMessage());
                    }
                }
                catch (Exception ex)
                {
                }
            });
             await _SecsMethod.Run(action, action1, action1);
        }

        private LabelInfo ReadLabel(string index,string id,VisionInspectionItem visionItem)
        {
            try
            {
                _LogInfoService.AddRunInfo(StationName + Message.开始读取标签.GetMessage());
                LabelInfo info = new LabelInfo();
                var d1 = _VisionMethod.GrabImage(id, StationName + "_" + index, visionItem);
                // 执行视觉检程序
                info.CurLabelInfo = _VisionMethod.ReadLabel(d1, id);
                info.CheckResult = EPLCResult.OK;
                return info;
            }
            catch (Exception ex)
            {
                _LogInfoService.AddRunInfo(ex.Message + ex.StackTrace, ELogLevel.SysError);
                throw ex;
            }
        }

        /// <summary>
        /// 贴后检测
        /// </summary>
        /// <param name="index"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task FitRead(string index,string id, VisionInspectionItem visionItem)
        {
            Func<Task> action = new Func<Task>( async() =>
            {
                curInfo = ReadLabelCompare(index,id,visionItem);
            });

            Func<Task> action1 = new Func<Task>(async () =>
            {
                curInfo = ReadLabelCompare(index, id, visionItem);

                if (curInfo.CheckResult != EPLCResult.OK)
                {
                    curInfo.CheckResult = EPLCResult.NG;
                    await _SecsMethod.SendS6F11(EventOrder.Label_Fitting_Fail);
                    _LogInfoService.AddRunInfo(StationName + Message.贴标签失败.GetMessage(), ELogLevel.Error);
                }
                else
                {
                    curInfo.CheckResult = EPLCResult.OK;
                    await _SecsMethod.SendS6F11(EventOrder.Label_Fitting_Success);
                    _LogInfoService.AddRunInfo(StationName + Message.贴标签成功.GetMessage());
                }
            });
            await _SecsMethod.Run(action, action1, action1);
        }

        /// <summary>
        /// 读取标签并进行对比
        /// </summary>
        /// <returns></returns>
        private LabelInfo ReadLabelCompare(string index,string id, VisionInspectionItem visionItem)
        {
            var d1 = ReadLabel(index, id, visionItem);
            //读码数据与之前进行对比
            if (d1.CheckResult != EPLCResult.OK) goto fail;
            if (curInfo.CurLabelInfo == null || d1.CurLabelInfo == null)
                throw new Exception("code is null");

            var r1 = curInfo.CurLabelInfo.Split(',').ToList();
            var r2 = d1.CurLabelInfo.Split(',').ToList();

            var b = ListEx.Equal<string>(r1, r2);
            if (b)
                d1.CheckResult = EPLCResult.OK;
            else
                goto fail;

            fail: d1.CheckResult = EPLCResult.NG;
            return d1;
        }
    }
}
