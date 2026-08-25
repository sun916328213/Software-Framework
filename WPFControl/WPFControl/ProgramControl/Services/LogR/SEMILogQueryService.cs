using DataManager.Log;
using Extension.Core.ControlEx;
using Extension.Core.FileEx;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WPFControl.Helper;
using WPFControl.Method;
using WPFControl.ProgramControl.Models;
using WPFCustomControls.Window.Views;

namespace WPFControl.ProgramControl.Services
{
    internal class SEMILogQueryService
    {
        /// <summary>
        /// 日志数据模型
        /// </summary>
        internal SEMILogQueryModel Model { get; set; } = new SEMILogQueryModel();

        internal readonly LogManager logManager;
        /// <summary>
        /// 日志助手
        /// </summary>
         private    ILog Service_SEMILog=> logManager["SEMILog"];

        public SEMILogQueryService(IContainerProvider container)
        {
            logManager= container.Resolve<LogManager>();
        }

        internal async Task Query_Log()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
           {
               DateTime pStart = Model.DateTime_Start;
               DateTime pEnd = Model.DateTime_End.AddDays(1);
               var d = await Service_SEMILog.Query_MultiAsync(  pStart, pEnd);
               Model.SEMILogDatas = new ObservableCollection<string>(d);
           });
        }

        internal async Task Save_Log()
        {
            await TryCatchHelper.TryExecuteAsync(async () =>
            {
                QuerySave querySave = new QuerySave();
                await querySave.QuerySaveCSVAsync(Model.SEMILogDatas);
            });
        }
    }
}
