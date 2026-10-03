// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：BulletinBoardViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、Prism.Ioc、System、System.Collections.Generic、System.Linq。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// BulletinBoardViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    public class BulletinBoardViewModel 
    {
        private readonly BulletinBoardService Service;

        public BulletinBoardModel Datas => Service.Model;

        public StatisicsModel StatisicsModel => Service.StatisticService.Model;

     

        /// <summary>
        /// 初始化 BulletinBoardViewModel 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
        /// </summary>
        public BulletinBoardViewModel(IContainerProvider container)
        {
            Service = container.Resolve<BulletinBoardService>();
        }
         
    }
}
