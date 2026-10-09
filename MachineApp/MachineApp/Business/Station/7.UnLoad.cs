// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：UnLoad。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Infra.Persistence.Entities、DataManager.Core.SQL、Extension.Core.Conversion、Extension.UI.Controls、DataManager.Core.Log.Definition。导入不代表所有类型都会在运行时加载。
using MachineApp.Infra.Persistence.Entities;
using DataManager.Core.SQL;
using Extension.Core.Conversion;
using Extension.UI.Controls;
using DataManager.Core.Log.Definition;
using MachineApp.Data;
using MachineApp.Business.Flow;
using MachineApp.Infra.Tool;
using MachineApp.Business.Order;

using System;
using System.Linq;
using MachineApp.UI.Model;

namespace MachineApp.Business.Station
{
    /// <summary>
    /// UnLoad 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// 继承或实现的契约：StationBase&lt;int&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public  class UnLoad :StationBase<int>
    {

        internal readonly Load _Load;
        internal readonly ReadID _ReadID;
        internal readonly Accessory _Accessory;
        internal readonly Label _Label;
        internal readonly HeatSeal _HeatSeal;

        internal readonly UnLoad _UnLoad;
        internal readonly ALDetect _ALDetect;
        internal readonly HumidityCard _HumidityCard;

        /// <summary>
        /// T11：主库（<see cref="CSQLData"/>）的 typed 获取函数。
        /// 存 <see cref="Func{TResult}"/> 而**不是**实例：站点实例可能先于启动脚本
        /// （<c>02_初始化工具</c>）构造，那一刻数据库还没 <c>Create</c>/<c>Connect</c>，
        /// 构造期取实例会直接抛。真正的取实例推迟到 <see cref="AddSQL"/>。
        /// </summary>
        internal readonly Func<ISQL<CSQLData>> _GetSql;

        /// <summary>
        /// 初始化 UnLoad 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 getLoad、getReadID、getAccessory、getLabel、getHeatSeal；具体条件和先后次序见方法体。
        /// </summary>
        public UnLoad(StationServices services,
            Func<Load> getLoad,
            Func<ReadID> getReadID,
            Func<Accessory> getAccessory,
            Func<Label> getLabel,
            Func<HeatSeal> getHeatSeal,
            Func<ALDetect> getALDetect,
            Func<HumidityCard> getHumidityCard,
            Func<ISQL<CSQLData>> getSql) : base(services)
        {
            _Load = getLoad();
            _ReadID = getReadID();
            _Accessory = getAccessory();
            _Label = getLabel();
            _HeatSeal = getHeatSeal();

            _ALDetect = getALDetect();
            _HumidityCard = getHumidityCard();

            _GetSql = getSql;
        }

        internal string StationName = EProcess.出站流程.ToString();

        /// <summary>
        /// Sensor触发
        /// </summary>
        public void Trigger( string pLCOrder)
        {
            EPLCResult result = EPLCResult.NG;
            string ID = null;

            try
            {
                _PLCMethod.TriggerReply(StationName, pLCOrder);
                ID = _PLCMethod.ReadData(StationName, pLCOrder, PLCAddressOffest.ID );
 
                Archive(ID);
                result = EPLCResult.OK;
            }
            catch (Exception ex)
            {
                result = EPLCResult.ERROR;
                string message = string.Join("\r\n", ex.Message, ex.StackTrace);
                _LogInfoService.AddRunInfo(StationName + ":" + message, ELogLevel.SysError);
            }
            finally
            {
                //通知PLC结果
                _PLCMethod.SendResult(StationName, pLCOrder, result);

                AddSQL(ID);
            }
        }
        /// <summary>
        /// 存档
        /// </summary>
        private void Archive(string ID)
        {
            try
            {
                //拍照
                var d1 = _VisionMethod.GrabImage(ID,StationName, GlobalData.VisionItem_Arch);
            
                _LogInfoService.AddRunInfo(StationName + Message.存档成功.GetMessage());
            }
            catch
            {
                _LogInfoService.AddRunInfo(StationName + Message.存档失败.GetMessage(), ELogLevel.Error);
            }
        }


        /// <summary>
        /// 记录数据
        /// </summary>
        public void AddSQL(string id)
        {

            // T11：按**类型**取主库实例，不再用 (ISQL<CSQLData>)_SQLManager[0]
            // 这种「注册序号即实体身份」的取法（序号会随 Create 顺序变化）。
            ISQL<CSQLData> db = _GetSql();
            //记录
            CSQLData sQLData = new CSQLData
            {
                ID = id,
                Time = DateTime.Now,
                StationInfo = new CStation
                {
                    Load = _Load.GetAndRemove(id),
                    ReadID = _ReadID.GetAndRemove(id),
                    Accessory = _Accessory.GetAndRemove(id),
                    ALDetect = _ALDetect.GetAndRemove(id),
                    HeatSeal = _HeatSeal.GetAndRemove(id),
                }
            };
            db.Add(sQLData);
           
            var check = db.Query(x => x.ID == id).FirstOrDefault();
            if (check != null)
            {
                _LogInfoService.AddRunInfo(" 数据库写入成功", ELogLevel.Information);
                _LogInfoService.AddRunInfo($" 数据库写入内容：\n ID:{check.ID}\n Time:{check.Time}\n Stationinfo:{check.StationInfo.ToJson()}");
            }
            else
            {
                _LogInfoService.AddRunInfo(" 数据库写入失败：未找到数据", ELogLevel.SysError);
            }
        }
    }
}
