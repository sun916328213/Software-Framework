using DataManager.Log;
using DataManager.Log.Definition;
using DataManager.SQL;
using DataManager.SQL.Definition;
using Packing_Machine.Data;
using Packing_Machine.Infra.Core;
using Packing_Machine.Infra.Tool;
using Packing_Machine.UI.Model;
using Secs;
using WPFControl.ProgramControl.Models;
using WPFControl.ProgramControl.Services;

public class Script
{

    private static PLCTool _PLCMethod;
    private static SecsControl _SecsControl;


    private static AlarmInfoManagerService _AlarmInfoService;

    private static LogManager _LogManager;
    private static SQLManager _SQLManager;


    public static void Initialize()
    {
        // 初始化逻辑
        _PLCMethod = ContainerManager.Resolve<PLCTool>();

        _SecsControl = ContainerManager.Resolve<SecsControl>();


        _AlarmInfoService = ContainerManager.Resolve<AlarmInfoManagerService>();


        _LogManager = ContainerManager.Resolve<LogManager>();
        _SQLManager = ContainerManager.Resolve<SQLManager>();
    }

    public static void Run()
    {
        Init_Tool();
    }

    private static void Init_Tool()
    {
        GlobalData.Load();

        Console.WriteLine("初始化Log日志");
        //日志
        _LogManager.Create(new LogParam()
        {
            FilePath = GlobalData.LogPath,
            Name = "Log",
            Separator = "||",
            SaveDays = 180,
            LogType = ELogType.SeriLog
        });

        Console.WriteLine("初始化SMEI日志");
        //SEMI日志
        _LogManager.Create(new LogParam()
        {
            FilePath = GlobalData.LogPath,
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