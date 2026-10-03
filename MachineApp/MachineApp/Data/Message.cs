// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Message。成员说明可结合下方具体实现阅读。
namespace MachineApp.Data
{
    /// <summary>
    /// Message 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
    /// </summary>
    public static class Message
    {
        public static readonly string[] 开始 = { "开始", "Start" };
        public static readonly string[] 结束 = { "结束", "End" };

        public static readonly string[] 相机拍照成功 = { "相机拍照成功", "Camera photo successful" };

        public static readonly string[] 九点标定成功 = { "九点标定成功", "9-point calibration successful" };
        public static readonly string[] 九点标定失败 = { "九点标定失败", "9-point calibration failed" };


        public static readonly string[] EAP回复校验成功 = { "EAP回复校验成功", "EAP response verification successful" };
        public static readonly string[] EAP回复校验失败 = { "EAP回复校验失败", "EAP response verification failed" };
        public static readonly string[] EAP回复超时 = { "EAP回复超时", "EAP response timeout" };
        public static readonly string[] PLC回复超时 = { "PLC回复超时", "PLC response timeout" };

        public static readonly string[] 存档成功 = { "存档成功", "Archive successful" };
        public static readonly string[] 存档失败 = { "存档失败", "Archive failed" };

        public static readonly string[] 不需要相机读取FOSBID = { "不需要相机读取FOSBID", "No need for camera to read FOSBID" };

        // 1
        public static readonly string[] FOSB上料成功 = { "FOSB上料成功", "FOSB feeding successful" };
        public static readonly string[] RFID开始读取 = { "RFID开始读取", "RFID starts reading" };
        public static readonly string[] RFID读取失败 = { "RFID读取失败", "RFID read failed" };
        public static readonly string[] RFID读取成功 = { "RFID读取成功", "RFID read successful" };

        public static readonly string[] 配方切换请求 = { "配方切换请求", "Recipe switching request" };
        public static readonly string[] 配方切换成功 = { "配方切换成功", "Recipe switching successful" };
        public static readonly string[] 配方切换失败 = { "配方切换失败", "Recipe switching failed" };

        //2
        public static readonly string[] 开始读取标签 = { "开始读取标签", "Start reading label" };
        public static readonly string[] FOSBID对比成功 = { "FOSBID对比成功", "FOSBID comparison successful" };
        public static readonly string[] FOSBID对比失败 = { "FOSBID对比失败", "FOSBID comparison failed" };

        //3
        public static readonly string[] 开始配件检测 = { "开始配件检测", "Start accessory inspection" };
        public static readonly string[] 配件检测成功 = { "配件检测成功", "Accessory inspection successful" };
        public static readonly string[] 配件检测失败 = { "配件检测失败", "Accessory inspection failed" };
        public static readonly string[] 湿度卡定位开始 = { "湿度卡定位开始", "Humidity card positioning started" };
        public static readonly string[] 湿度卡相机基准示教 = { "湿度卡相机基准示教", "Humidity card camera base teaching" };
        public static readonly string[] 湿度卡颜色传感器基准示教 = { "湿度卡颜色传感器基准示教", "Humidity card color sensor base teaching" };
        //4
        public static readonly string[] 打印标签 = { "打印标签", "Print label" };
        public static readonly string[] 贴标签成功 = { "贴标签成功", "Label贴附 successful" };
        public static readonly string[] 贴标签失败 = { "贴标签失败", "Label贴附 failed" };

        //5
        public static readonly string[] 抽真空流程开始 = { "抽真空流程开始", "Vacuum process started" };
        public static readonly string[] 充氮气流程开始 = { "充氮气流程开始", "Nitrogen filling process started" };
        public static readonly string[] 热封流程开始 = { "热封流程开始", "Heat sealing process started" };
        public static readonly string[] 触发热封相机拍照 = { "触发热封相机拍照", "Trigger heat sealing camera photo" };
        public static readonly string[] 热封相机拍照成功 = { "热封相机拍照成功", "Heat sealing camera photo successful" };
        public static readonly string[] 热封相机拍照失败 = { "热封相机拍照失败", "Heat sealing camera photo failed" };

        //6
        public static readonly string[] 开始AL袋定位 = { "开始AL袋定位", "Start AL bag positioning" };

        //7
        public static readonly string[] FOSB下料成功 = { "FOSB下料成功", "FOSB unloading successful" };

        //流程
        public static readonly string[] 流程 = { "流程", "Flow" };
        public static readonly string[] 自动流程 = { "自动流程", "Auto Flow" };
        public static readonly string[] 初始化流程 = { "初始化流程", "Initialize Flow" };
        public static readonly string[] 启动流程 = { "启动流程", "Startup Flow" };
        public static readonly string[] 关闭流程 = { "关闭流程", "Shutdown Flow" };

        //显示
        public static readonly string[] 存档站 = { "存档站", "Archive Station" };
        public static readonly string[] 热封站 = { "热封站", "Heat Sealing Station" };
        public static readonly string[] 复检站 = { "复检站", "Reinspection Station" };
        public static readonly string[] AT_Left站 = { "AT_Left站", "AT Left Station" };
        public static readonly string[] AT_Right站 = { "AT_Right站", "AT Right Station" };
        public static readonly string[] 标签站 = { "标签站", "Label Station" };

        public static readonly string[] 主页 = { "主页", "Home" };
        public static readonly string[] 视觉查看 = { "视觉查看", "Vision View" };
        public static readonly string[] 配方 = { "配方", "Recipe" };
        public static readonly string[] 打印设置 = { "打印设置", "Print Settings" };
        public static readonly string[] 检测项设置 = { "检测项设置", "InspectionItem Settings" };
        public static readonly string[] 视觉设置 = { "视觉设置", "Vision Settings" };
        public static readonly string[] SEMI设置 = { "SEMI设置", "SEMI Settings" };
        public static readonly string[] RFID设置 = { "RFID设置", "RFID Settings" };
        public static readonly string[] Cam设置 = { "Cam设置", "Camera Settings" };
        public static readonly string[] 用户登录 = { "用户登录", "User Login" };
        public static readonly string[] 用户信息管理 = { "用户信息管理", "User Info Manager" };
        public static readonly string[] Log查询 = { "Log查询", "Log Query" };
        public static readonly string[] SEMILog查询 = { "SEMILog查询", "SEMI Log Query" };
       
        public static readonly string[] 图像查询 = { "图像查询", "Image Query" };
        public static readonly string[] 报警查询 = { "报警查询", "Alarm Query" };
        public static readonly string[] 报警设置 = { "报警设置", "Alarm Settings" };

        public static readonly string[] 硬件设置 = { "硬件设置", "Hardware  Settings" };
        public static readonly string[] 数据查询 = { "数据查询", "Data Query" };
    }
}