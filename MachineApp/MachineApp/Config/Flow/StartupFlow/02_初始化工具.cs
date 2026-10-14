// 【中文阅读说明】
// MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
// 本文件定义：Script。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：MachineApp.Infra.Persistence.Entities、DataManager.Core.Log、DataManager.Runtime.Log、DataManager.Core.Log.Definition、DataManager.Core.SQL、DataManager.Runtime.SQL。导入不代表所有类型都会在运行时加载。
using MachineApp.Infra.Persistence.Entities;
using DataManager.Core.Log;
using DataManager.Runtime.Log;
using DataManager.Core.Log.Definition;
using DataManager.Core.SQL;
using DataManager.Runtime.SQL;
using DataManager.Core.SQL.Definition;
using DataManager.Runtime.SQL.Definition;
using MachineApp.Data;
using MachineApp.Infra.Core;
using MachineApp.Infra.Tool;
using MachineApp.UI.Model;
using WPFControls.ProgramControl.Models;
using WPFControls.ProgramControl.Services;

/// <summary>
/// Script 的类型定义。MachineApp 模块：具体机型的应用宿主，负责机型业务、导航选择、数据库实体和生产设备生命周期。
/// </summary>
public class Script
{

    private static PLCTool _PLCMethod;


    private static AlarmInfoManagerService _AlarmInfoService;

    private static ILogManager _LogManager;
    private static SQLManager _SQLManager;


    /// <summary>
    /// 执行初始化步骤；对应入口为 Initialize。
    /// 正文中的关键协作调用包括 Resolve；具体条件和先后次序见方法体。
    /// </summary>
    public static void Initialize()
    {
        // 初始化逻辑
        _PLCMethod = ContainerManager.Resolve<PLCTool>();



        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();


        _LogManager = ContainerManager.Resolve<ILogManager>();
        _SQLManager = ContainerManager.Resolve<SQLManager>();
    }

    /// <summary>
    /// 运行当前处理流程；对应入口为 Run。
    /// 正文中的关键协作调用包括 Init_Tool；具体条件和先后次序见方法体。
    /// </summary>
    public static void Run()
    {
        Init_Tool();
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 Init_Tool。
    /// 正文中的关键协作调用包括 Load、WriteLine、Create、Connect；具体条件和先后次序见方法体。
    /// </summary>
    private static void Init_Tool()
    {
        GlobalData.Load();

        Console.WriteLine("初始化Log日志");
        // GlobalData.LogPath 从未实现过（git log -S 无任何记录）。改用唯一生产先例
        // LogWorkspace.cs:332 的 Path.Combine(BaseDirectory, "logs") —— 日志落盘位置
        // 不能依赖工作目录，工厂现场从非 exe 目录启动是常见翻车点。
        //日志
        _LogManager.Create(new LogParam()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            Name = "Log",
            Separator = "||",
            SaveDays = 180,
            LogType = ELogType.SeriLog
        });

        Console.WriteLine("初始化SMEI日志");
        //SEMI日志
        _LogManager.Create(new LogParam()
        {
            FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"),
            Name = "SEMILog",
            Separator = "||",
            SaveDays = 180,
            LogType = ELogType.SeriLog
        });

        Console.WriteLine("初始化数据库");
        _SQLManager.Create<CSQLData>(new SQLParam()
        {
            ServerIP = "localhost",
            Port = "5432",
            UserName = "postgres",
            Password = "123",
            DBName = "test1",
            DBType = ESQLType.PostgreSQL,
            ToolType = ESQLToolType.EFCore,
        });

        Console.WriteLine("连接数据库");
        //连接数据库
        _SQLManager[0].Connect();
        Console.WriteLine("连接数据库成功");
        
        Console.WriteLine("初始化数据库");
        _SQLManager.Create<CapacityStatisticsInfo>(new SQLParam()
        {
            ServerIP = "localhost",
            Port = "5432",
            UserName = "postgres",
            Password = "123",
            DBName = "产能数据库",
            DBType = ESQLType.PostgreSQL,
            ToolType = ESQLToolType.EFCore,
        });

        Console.WriteLine("连接数据库");
        //连接数据库
        _SQLManager[1].Connect();
        Console.WriteLine("连接数据库成功");
    }
}
