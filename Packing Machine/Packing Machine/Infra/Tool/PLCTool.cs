using Extension.UI.Controls;
using DataManager.Log.Definition;
using Packing_Machine.Data;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using Secs;
using WPFControl.CustomControl.Models;
using WPFControl.ProgramControl.Services;
using Packing_Machine.Business.Station;

namespace Packing_Machine.Infra.Tool
{
    public static class PLCAddressOffest
    {
        public static int Trigger = 0;
        public static int Reply = 1;
        public static int Result = 2;
        public static int Function = 3;
        public static int Count = 4;
        public static int CountReply = 5;

        public static int ID = 10;
    }

    /// <summary>
    /// PLC 业务入口。
    /// <para>
    /// ⚠️ 当前版本**没有可用的 PLC 实现**。旧实现依赖已删除的
    /// CommunicationManager / MCHelper（旧 Communication 工程），
    /// 本类只保留类型与全部业务方法签名，任何调用都会抛出
    /// <see cref="NotSupportedException"/> —— 明确失败，不静默、不伪造数据。
    /// </para>
    /// <para>
    /// 迁移方向：新建 PlcService/PlcRuntime，依赖 Communication.Core 的
    /// McClientDevice + PlcTagDefinition/PlcTagCache/PlcPollingService，
    /// 详见《Software-Framework_Communication_Device重构删除实施说明》第 10 章。
    /// </para>
    /// </summary>
    public class PLCTool
    {
        private const string MigrationMessage =
            "Packing Machine 的 PLC 能力已随旧通信栈（CommunicationManager/MCHelper）删除，"
            + "当前不可用。请迁移到 Communication.Core 的 McClientDevice/DataPoint 体系后再调用"
            + "（详见实施说明第 10 章）。";

        readonly IContainerProvider _container;
        LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();
        readonly SecsControl _SecsControl;

        public PLCTool(IContainerProvider container)
        {
            _container = container;
            _SecsControl = container.Resolve<SecsControl>();
        }

        /// <summary>
        /// 刷新任务
        /// </summary>
        public void Refresh() => throw new NotSupportedException(MigrationMessage);

        public void StopRefresh() => throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 触发回复
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="pPLCOrder"></param>
        public void TriggerReply(string stationName, string pPLCOrder) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <param name="info"></param>
        public void SendData(string sationName, string pPLCOrder, int OffestAdd, object info) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 发送结果
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void SendResult(string sationName, string pPLCOrder, EPLCResult info) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 流程结束
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="info"></param>
        public void Complete(string sationName, string pPLCOrder) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 所有流程结束
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="length"></param>
        public void CompleteAll(string sationName, string pPLCOrder, int length) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="sationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="OffestAdd"></param>
        /// <returns></returns>
        public string ReadData(string sationName, string pPLCOrder, int OffestAdd = 0, bool islog = true) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 写入用户信息
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerUserInfo(UserInfo value) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 写入报警信息
        /// </summary>
        /// <param name="pPLCOrder"></param>
        /// <param name="value"></param>
        public void TriggerAlarm(string pPLCOrder, short value) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 触发PLC
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public Task<EPLCResult> TriggerAsync(string stationName, string pPLCOrder, string data) =>
            throw new NotSupportedException(MigrationMessage);

        /// <summary>
        /// 添加日志。保留原实现：PLC 读写已禁用，但日志通道本身与旧通信栈无关，
        /// 后续迁移后的 PlcService 仍可复用同一格式。
        /// </summary>
        /// <param name="stationName"></param>
        /// <param name="fun"></param>
        /// <param name="pPLCOrder"></param>
        /// <param name="offestAdd"></param>
        /// <param name="value"></param>
        private void AddLog(string stationName, string fun, string pPLCOrder, int offestAdd, string value)
        {
            stationName = stationName.Replace(":","").Trim() + ": ";
            _LogInfoService.AddRunInfo(stationName + fun + " " + "地址:" + pPLCOrder + " " + "偏移:" + offestAdd + " " + "数据:" + value);
        }
    }
}
