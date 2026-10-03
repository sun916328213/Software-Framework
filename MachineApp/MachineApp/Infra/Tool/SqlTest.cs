// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。 本文件位于适配边界，负责把外部能力接到已有契约，不在此引入机型业务规则。
// 本文件定义：DbTestManager。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Infra.Persistence.Entities、DataManager.Core.SQL、DataManager.Runtime.SQL、DataManager.Runtime.SQL.EFCore、Extension.Platform.Diagnostics、Extension.Platform.Native。导入不代表所有类型都会在运行时加载。
using MachineApp.Infra.Persistence.Entities;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Runtime.SQL.EFCore;
using Extension.Platform.Diagnostics;
using Extension.Platform.Native;
using Extension.Platform.Memory;
using Extension.Platform.Processes;
using Microsoft.EntityFrameworkCore;
using MachineApp.Data;
using MachineApp.Business.Station;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using WPFControls.ProgramControl.Services;
using MachineApp.UI.Model;

namespace MachineApp.Infra.Tool
{
    /// <summary>
    /// 数据库测试
    /// </summary>
    public class DbTestManager
    {
        private readonly SQLManager _sqlManager;
        internal   LogInfoService _LogInfoService => _container.Resolve<LogInfoService>();
        // 各站点实例
        private readonly Load _load;
        private readonly ReadID _readID;
        private readonly Accessory _accessory;
        private readonly ALDetect _alDetect;
        private readonly IContainerProvider _container;
        private readonly HeatSeal _heatSeal;
        private readonly UnLoad _unload;
 
        /// <summary>
        /// 初始化 DbTestManager 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public DbTestManager(IContainerProvider container)
        {
            _container = container;
            _sqlManager = container.Resolve<SQLManager>();
            // 初始化所有站点实例
            _load = container.Resolve<Load>();
            _readID = container.Resolve<ReadID>();
            _accessory = container.Resolve<Accessory>();
            _alDetect = container.Resolve<ALDetect>();
            _heatSeal = container.Resolve<HeatSeal>();
            _unload = container.Resolve<UnLoad>();
        }

        /// <summary>
        /// 生成并写入所有站点的测试数据
        /// </summary>
        public void WriteTestDataToDb()
        {
            try
            {
                var ef = _sqlManager[0] as ISQL<CSQLData>;
            
                var testSqlData = new CSQLData
                {
                    ID = "test",
                    Time = DateTime.Now, // 创建时间
                    StationInfo = new CStation
                    {
                        // Load站点测试数据
                        Load = new LoadInfo
                        {
                            RFIDInfo = "test",
                            CheckResult = EPLCResult.OK,
                        },
                        // ReadID站点测试数据
                        ReadID = new FOSBInfo
                        {
                            FOSBID = "test",
                            CheckResult = EPLCResult.OK,
                        },
                        // Accessory站点测试数据
                        Accessory = new AccessoryInfo
                        {
                            Num_Desiccant = 2, // 干燥剂数量
                            Num_HumidityIndicator = 1, // 湿度卡数量
                            CheckResult = EPLCResult.OK,
                        },
                        // ALDetect站点测试数据
                        ALDetect = new ALInfo
                        {
                            Width = 500.5, // 宽度
                            Angle = 1.2, // 角度
                            CheckResult = EPLCResult.OK,
                        },
                        // HeatSeal站点测试数据
                        HeatSeal = new HeatSealInfo
                        {
                            Index = new List<int> { 1, 3 }, // NG图像ID列表
                            CheckResult = EPLCResult.OK,
                        },
                    },
                    
                };
                SyncTestDataToStationCollections(testSqlData);

                ef.Delete (d => d.ID == "test");

                _unload.AddSQL(testSqlData.ID);
                
              
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试数据写入失败：{ex.Message}\r\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// 将测试数据同步到各站点
        /// </summary>
        private void SyncTestDataToStationCollections(CSQLData testData)
        {
            _load.AddUpdate(testData.ID, testData.StationInfo.Load);
            _readID.AddUpdate(testData.ID, testData.StationInfo.ReadID);
            _accessory.AddUpdate(testData.ID, testData.StationInfo.Accessory);
            _alDetect.AddUpdate(testData.ID, testData.StationInfo.ALDetect);
            _heatSeal.AddUpdate(testData.ID, testData.StationInfo.HeatSeal);
        }
    }

}