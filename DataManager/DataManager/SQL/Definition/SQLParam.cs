using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace DataManager.SQL.Definition
{
    /// <summary>
    /// SQL 连接参数（可观察模型：属性变更广播 PropertyChanged，工作台表格可直绑）。
    /// 生成属性名与历史自动属性完全一致，JSON 序列化、指纹、Validate 全兼容。
    /// </summary>
    public sealed partial class SQLParam : ObservableObject
    {
        /// <summary>
        /// 名称
        /// </summary>
        [ObservableProperty]
        private string _name = string.Empty;

        /// <summary>
        /// 数据库服务器IP
        /// </summary>
        [ObservableProperty]
        private string _serverIP = string.Empty;

        /// <summary>
        /// 数据库端口
        /// </summary>
        [ObservableProperty]
        private string _port = string.Empty;

        /// <summary>
        /// 数据库名称
        /// </summary>
        [ObservableProperty]
        private string _dBName = string.Empty;

        /// <summary>
        /// 数据库用户名
        /// </summary>
        [ObservableProperty]
        private string _userName = string.Empty;

        /// <summary>
        /// 数据库密码
        /// </summary>
        [ObservableProperty]
        private string _password = string.Empty;

        /// <summary>
        /// SQLite 数据库文件路径（仅 SQLite 使用；可为相对或绝对路径）。
        /// 与 <see cref="DBName"/> 任填其一即可，优先使用本字段。
        /// </summary>
        [ObservableProperty]
        private string _filePath = string.Empty;

        /// <summary>
        /// 完整连接串直通（可选，优先级最高）。
        /// 非空时各 Helper 优先直接使用，不再按分字段拼串；仍要求 <see cref="DBType"/> 正确。
        /// </summary>
        [ObservableProperty]
        private string _connectionString = string.Empty;

        /// <summary>
        /// SQL Server 是否信任服务器证书（默认 true，开发/内网自签证书场景免配置）。
        /// </summary>
        [ObservableProperty]
        private bool _trustServerCertificate = true;

        /// <summary>
        /// 数据库类型
        /// </summary>
        [ObservableProperty]
        private ESQLType _dBType = ESQLType.PostgreSQL;

        /// <summary>
        /// 数据库工具类型
        /// </summary>
        [ObservableProperty]
        private ESQLToolType _toolType = ESQLToolType.EFCore;

        /// <summary>
        /// P9-4：统一命令执行超时（秒）。默认 0 = 保持 provider 默认行为（升级后不改变现场超时）。
        /// 只控制命令执行（Dapper CommandDefinition.CommandTimeout / EFCore CommandTimeout /
        /// SqlSugar Ado.CommandTimeOut），不修改 ConnectionString 直通串里的 Connect Timeout。
        /// </summary>
        [ObservableProperty]
        private int _commandTimeoutSeconds = 0;

        /// <summary>
        /// P9-4：命令超时映射为「provider 原生口径」：0 表示不设置（null = provider 默认），否则为秒数。
        /// </summary>
        public int? CommandTimeoutOrNull => CommandTimeoutSeconds > 0 ? CommandTimeoutSeconds : null;

        /// <summary>
        /// P2-3：参数校验，缺失必填项抛 ArgumentException。
        /// P3-1：按库类型分支校验。直通连接串非空时仅校验 Name + DBType；
        /// SQLite 要求 FilePath/DBName 任一非空；SQL Server 端口可选；其余库保持原规则。
        /// P9-4：额外校验 CommandTimeoutSeconds 非负（0 合法，表示 provider 默认）。
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("SQLParam.Name 不能为空。", nameof(Name));
            if (CommandTimeoutSeconds < 0)
                throw new ArgumentException("SQLParam.CommandTimeoutSeconds 不能为负（0 表示 provider 默认）。", nameof(CommandTimeoutSeconds));
            if (!string.IsNullOrWhiteSpace(ConnectionString))
                return;
            if (DBType == ESQLType.SQLite)
            {
                if (string.IsNullOrWhiteSpace(FilePath) && string.IsNullOrWhiteSpace(DBName))
                    throw new ArgumentException("SQLite 要求 SQLParam.FilePath（或 DBName）不能为空。", nameof(FilePath));
                return;
            }
            if (string.IsNullOrWhiteSpace(ServerIP))
                throw new ArgumentException("SQLParam.ServerIP 不能为空。", nameof(ServerIP));
            if (DBType != ESQLType.SQL_Server && string.IsNullOrWhiteSpace(Port))
                throw new ArgumentException("SQLParam.Port 不能为空。", nameof(Port));
            if (string.IsNullOrWhiteSpace(DBName))
                throw new ArgumentException("SQLParam.DBName 不能为空。", nameof(DBName));
            if (string.IsNullOrWhiteSpace(UserName))
                throw new ArgumentException("SQLParam.UserName 不能为空。", nameof(UserName));
            if (Password == null)
                throw new ArgumentException("SQLParam.Password 不能为空（可为空字符串）。", nameof(Password));
        }

        /// <summary>
        /// SQLite 有效文件路径：优先 FilePath，其次把 DBName 当作文件路径兼容。
        /// </summary>
        public string EffectiveFilePath =>
            !string.IsNullOrWhiteSpace(FilePath) ? FilePath : DBName;
    }
}
