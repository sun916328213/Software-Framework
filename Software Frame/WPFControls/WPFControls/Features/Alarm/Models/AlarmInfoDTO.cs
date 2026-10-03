// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：AlarmInfoDTO。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls。导入不代表所有类型都会在运行时加载。
using WPFControls;
namespace WPFControls.ProgramControl.Models;

/// <summary>
/// AlarmInfoDTO 的类型定义。WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
/// </summary>
public class AlarmInfoDTO
    {
        public int ID { get; set; }
        public string Info { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public string Duration { get; set; }
    }
