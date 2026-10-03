// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoManagerViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、Prism.Ioc、Prism.Regions、System、System.Collections.Generic。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using Prism.Ioc;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Alarm
{
    /// <summary>
    /// AlarmInfoManagerViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    internal class AlarmInfoManagerViewModel
    {
        private readonly AlarmInfoManagerService Service;

        public AlarmInfoManagerModel Datas => Service.Model;

        public Action SaveCallbackCommand { get; }

        /// <summary>
        /// 初始化 AlarmInfoManagerViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve、Save；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public AlarmInfoManagerViewModel(IContainerProvider container)
        {
             Service = container.Resolve<AlarmInfoManagerService>();

            SaveCallbackCommand = (async () =>
            {
              await  Service.Save();
            });
        }
 
    }
}
