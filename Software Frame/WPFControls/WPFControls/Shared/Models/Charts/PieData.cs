// 【中文阅读说明】
// WPFControls 模块：可复用业务界面及模块装配，页面可用性与机型实际选择分离，具体数据库实体和硬件启动归宿主。
// 本文件定义：PieData。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：WPFControls、WPFControls.Controls.Models、System.Windows.Media。导入不代表所有类型都会在运行时加载。
using WPFControls;
using WPFControls.Controls.Models;
using System.Windows.Media;

namespace WPFControls.Controls.Models.Charts;

/// <summary>
    /// 饼状图数据
    /// </summary>
    public class PieData
    {
        /// <summary>
        /// 值
        /// </summary>
        public double  Value { get; set; }

        /// <summary>
        /// Series Name
        /// </summary>
        public string SeriesName { get; set; }
        /// <summary>
        /// 颜色
        /// </summary>
        public SolidColorBrush Color { get; set; }
    }
