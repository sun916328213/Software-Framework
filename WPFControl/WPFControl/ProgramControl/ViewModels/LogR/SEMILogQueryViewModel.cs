using CommunityToolkit.Mvvm.Input;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

namespace WPFControl.ProgramControl.ViewModels
{
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
