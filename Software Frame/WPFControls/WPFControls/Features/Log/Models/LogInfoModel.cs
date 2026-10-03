// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：LogInfoModel。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、DataManager.Core.Log.Definition、WPFCustomControls.Controls。导入不代表所有类型都会在运行时加载。
using WPFControls;
using DataManager.Core.Log.Definition;
using WPFCustomControls.Controls;

namespace WPFControls.ProgramControl.Models
{
    /// <summary>
    /// LogInfoModel 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
    /// 继承或实现的契约：DataGridModel&lt;LogInfo&gt;。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public class LogInfoModel : DataGridModel<LogInfo>
    {
     
    }
}