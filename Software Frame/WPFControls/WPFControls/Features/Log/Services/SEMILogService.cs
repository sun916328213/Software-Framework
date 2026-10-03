// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMILogService。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Log、DataManager.Runtime.Log、Prism.Ioc、WPFCustomControls.Controls、WPFControls.Controls.Diagnostics。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using Prism.Ioc;
using WPFCustomControls.Controls;
using WPFControls.Controls.Diagnostics;

namespace WPFControls.ProgramControl.Services
{
    /// <summary>
    /// SEMILogService 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：CustListViewService。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class SEMILogService : CustListViewService
    {
        private readonly TryCatchHelper _errors;

        
        /// <summary>
        /// 初始化 SEMILogService 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public SEMILogService(IContainerProvider container, TryCatchHelper? errors = null)
        {
            _errors = errors ?? new();

            
        }


        /// <summary>
        /// 添加运行信息到控件显示和日志文件
        /// </summary>
        /// <param name="info">日志信息</param>

        public void AddRunInfo(string info)
        {
            _errors.TryExecute(() =>
            {
                Add(info);
            });
        }
    }
}
