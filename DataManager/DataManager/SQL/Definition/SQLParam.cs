using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataManager.SQL.Definition
{
    public class SQLParam
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 数据库服务器IP
        /// </summary>
        public string ServerIP { get; set; } = string.Empty;
        /// <summary>
        /// 数据库端口
        /// </summary>
        public string Port { get; set; } = string.Empty;
        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DBName { get; set; } = string.Empty;
        /// <summary>
        /// 数据库用户名
        /// </summary>
        public string UserName { get; set; } = string.Empty;
        /// <summary>
        /// 数据库密码
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 数据库类型
        /// </summary>
        public ESQLType DBType { get; set; }= ESQLType.PostgreSQL;
        /// <summary>
        /// 数据库工具类型
        /// </summary>
        public ESQLToolType ToolType { get; set; }= ESQLToolType.EFCore;

        /// <summary>
        /// P2-3：参数校验，缺失必填项抛 ArgumentException。
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("SQLParam.Name 不能为空。", nameof(Name));
            if (string.IsNullOrWhiteSpace(ServerIP))
                throw new ArgumentException("SQLParam.ServerIP 不能为空。", nameof(ServerIP));
            if (string.IsNullOrWhiteSpace(Port))
                throw new ArgumentException("SQLParam.Port 不能为空。", nameof(Port));
            if (string.IsNullOrWhiteSpace(DBName))
                throw new ArgumentException("SQLParam.DBName 不能为空。", nameof(DBName));
            if (string.IsNullOrWhiteSpace(UserName))
                throw new ArgumentException("SQLParam.UserName 不能为空。", nameof(UserName));
            if (Password == null)
                throw new ArgumentException("SQLParam.Password 不能为空（可为空字符串）。", nameof(Password));
        }
    }
}
