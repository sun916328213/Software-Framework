// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：SEMILogQueryViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls.ProgramControl.ViewModels、WPFControls、CommunityToolkit.Mvvm.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls.ProgramControl.ViewModels;
using WPFControls;
using CommunityToolkit.Mvvm.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

namespace WPFControls.ProgramControl.ViewModels.Log
{
    /// <summary>
    /// SEMILogQueryViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// </summary>
    internal class SEMILogQueryViewModel
    {
        /// <summary>
        /// 日志查询服务
        /// </summary>
        private readonly SEMILogQueryService Service;
 
        public SEMILogQueryModel Datas => Service.Model;

        /// <summary>
        /// 查询命令
        /// </summary>
        public RelayCommand QueryCommand { get; }

 
        /// <summary>
        /// 保存命令
        /// </summary>
        public RelayCommand SaveCommand { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="service">日志查询服务</param>
        public SEMILogQueryViewModel(SEMILogQueryService service)
        {
            Service = service;

            // 初始化查询命令
            QueryCommand = new RelayCommand(  async () =>
            {
                await  Service.Query_Log();
            });
 
            // 初始化保存命令
            SaveCommand = new RelayCommand(async () =>
            {
                await Service.Save_Log();
            });
        }
    }
}
