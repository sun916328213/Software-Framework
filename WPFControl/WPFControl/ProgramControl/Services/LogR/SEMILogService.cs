using DataManager.Log;
using Prism.Ioc;
using WPFCustomControls.Controls;
using WPFControl.Helper;

namespace WPFControl.ProgramControl.Services
{
    public class SEMILogService : CustListViewService
    {
        
        public SEMILogService(IContainerProvider container)
        {
            
        }


        /// <summary>
        /// 添加运行信息到控件显示和日志文件
        /// </summary>
        /// <param name="info">日志信息</param>

        public void AddRunInfo(string info)
        {
            TryCatchHelper.TryExecute(() =>
            {
                Add(info);
            });
        }
    }
}
