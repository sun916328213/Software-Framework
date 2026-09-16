using DataManager.SQL;
using Extension.Core.ConvertEx;
using Extension.Core.SystemEx;
using Extension.Struct;
using Packing_Machine.Data;
using Packing_Machine.Business.Flow;
using Packing_Machine.Infra.Tool;
using Packing_Machine.Business.Order;
using Prism.Ioc;
using System;
using System.Linq;
using Packing_Machine.UI.Model;

namespace Packing_Machine.Business.Station
{
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
        internal readonly SQLManager _SQLManager;

        public UnLoad(IContainerProvider container ) : base(container)
        {
            _Load = container.Resolve<Load>();
            _ReadID = container.Resolve<ReadID>();
            _Accessory = container.Resolve<Accessory>();
            _Label = container.Resolve<Label>();
            _HeatSeal = container.Resolve<HeatSeal>();

            _ALDetect = container.Resolve<ALDetect>();
            _HumidityCard = container.Resolve<HumidityCard>();

            _SQLManager = container.Resolve<SQLManager>();
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

            //_SQLManager[0].DeleteAll<CSQLData>();

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
            ISQL<CSQLData> db = (ISQL<CSQLData>) _SQLManager[0];
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

