// 【中文阅读说明】
// 信任层类型：清单判定结果、签发密钥记录、keyId 解析结果。
// 信任清单本体见 TrustManifest.cs。
using System;
using System.Collections.Generic;

namespace Extension.Security.Licensing
{
    /// <summary>清单整体校验结果。</summary>
    public enum TrustStatus
    {
        /// <summary>根签名有效、版本不倒退、未整体过期。</summary>
        Ok,
        /// <summary>结构非法：编码错误、字段缺失或多余、时间格式错误、keyId 与公钥不符。</summary>
        Malformed,
        /// <summary>清单不是这个根签的，或内容被改过。</summary>
        Unsigned,
        /// <summary>清单声明的根公钥与宿主钉住的不是同一把。</summary>
        RootMismatch,
        /// <summary>清单版本低于本机已接受的最高版本：回滚尝试。</summary>
        Stale,
        /// <summary>清单整体已过期。</summary>
        Expired,
        /// <summary>清单签发时间明显晚于本机时间：时钟或清单不可信。</summary>
        ClockInvalid,
    }

    /// <summary>按 keyId 解析签发密钥的结果。</summary>
    public enum TrustKeyStatus
    {
        /// <summary>在册，且当前时间落在有效窗口内。</summary>
        Active,
        /// <summary>不在册：已吊销，或从未授权。</summary>
        Unknown,
        /// <summary>生效时间还没到。</summary>
        NotYetValid,
        /// <summary>有效窗口已过：计划内轮换或到期。</summary>
        Expired,
    }

    /// <summary>清单中的一把签发密钥。KeyId 由公钥派生，不接受外部声明。</summary>
    public sealed class TrustKeyRecord
    {
        public string KeyId { get; init; } = string.Empty;
        public string PublicKey { get; init; } = string.Empty;
        public DateTimeOffset NotBefore { get; init; }
        public DateTimeOffset NotAfter { get; init; }
    }
}
