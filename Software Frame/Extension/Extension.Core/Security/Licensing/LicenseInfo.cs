// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：LicenseInfo、LicenseStatus、LicenseVerifyResult。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Text.Json.Serialization。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Extension.Security.Licensing
{
    /// <summary>
    /// 授权 License 数据（JSON 序列化后由 RSA 私钥签名）
    /// </summary>
    public sealed class LicenseInfo
    {
        /// <summary>授权单号</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>客户名称</summary>
        public string Customer { get; set; } = string.Empty;

        /// <summary>绑定的机器码（空 = 不绑定，任意机器可用）</summary>
        public string MachineCode { get; set; } = string.Empty;

        /// <summary>签发时间（UTC）</summary>
        public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>过期时间（UTC，MaxValue = 永不过期）</summary>
        public DateTime ExpireAtUtc { get; set; } = DateTime.MaxValue;

        /// <summary>功能点列表（空 = 全部功能）</summary>
        public List<string> Features { get; set; } = new();

        /// <summary>扩展字段（限点数、版本号上限等）</summary>
        public Dictionary<string, string> Extra { get; set; } = new();

        [JsonIgnore]
        public bool IsPermanent => ExpireAtUtc >= DateTime.MaxValue.AddDays(-1);
    }

    /// <summary>
    /// License 校验结果
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>合法有效</summary>
        Valid,
        /// <summary>签名无效（被篡改或公钥不匹配）</summary>
        InvalidSignature,
        /// <summary>格式错误</summary>
        Malformed,
        /// <summary>已过期</summary>
        Expired,
        /// <summary>机器码不匹配</summary>
        MachineMismatch,
        /// <summary>缺少所需功能点</summary>
        FeatureDenied,
    }

    /// <summary>
    /// License 校验返回体
    /// </summary>
    public sealed class LicenseVerifyResult
    {
        public LicenseStatus Status { get; init; }
        public LicenseInfo? License { get; init; }
        public string Message { get; init; } = string.Empty;
        public bool Ok => Status == LicenseStatus.Valid;
    }
}
