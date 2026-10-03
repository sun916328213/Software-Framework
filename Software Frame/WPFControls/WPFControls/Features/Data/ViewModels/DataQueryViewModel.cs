// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：DataQueryViewModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、CommunityToolkit.Mvvm.ComponentModel、CommunityToolkit.Mvvm.Input、WPFControls.ProgramControl.Models、WPFControls.ProgramControl.Services。导入不代表所有类型都会在运行时加载。
using WPFControls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;


namespace WPFControls.ProgramControl.ViewModels
{
    /// <summary>
    /// DataQueryViewModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：ObservableObject。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class DataQueryViewModel<T, TDTO> : ObservableObject where T : ISQLDataBase
    {
        // 通过构造函数注入Model_DataQuery
        public readonly DataQueryService<T, TDTO> Service;

        public DataQueryModel<T, TDTO> Datas => Service.Model;


        public RelayCommand QueryCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }


        // 构造函数注入
        public DataQueryViewModel(DataQueryService<T, TDTO> model)
        {
            Service = model;

            QueryCommand = new RelayCommand(() =>
            {
                Service.Query_DB();
            });

            SaveCommand = new AsyncRelayCommand(async () =>
            {
              await  Service.Save_DB();
            });
        }
    }
}