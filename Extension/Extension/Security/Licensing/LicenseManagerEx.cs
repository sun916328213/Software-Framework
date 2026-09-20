using Extension.Security.Cryptography;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Extension.Security.Licensing
{
    /// <summary>
    /// 软件授权管理（RSA 签名 License + 机器码绑定 + 有效期 + 功能点）
    /// 流程：厂商用私钥签发 .lic 文件 → 客户端用公钥验签。
    /// 私钥绝不进客户端程序集。
    /// License 文件格式：Base64Url(payloadJson).Base64Url(signature)，单行文本。
    /// </summary>
    public static class LicenseManagerEx
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// 签发 License（厂商工具调用，需私钥）
        /// </summary>
        public static string Create(LicenseInfo info, string privateKeyBase64)
        {
            string payloadJson = JsonSerializer.Serialize(info, JsonOptions);
            byte[] payload = Encoding.UTF8.GetBytes(payloadJson);
            byte[] sig;
            using (var rsa = RsaEx.FromPrivateKey(privateKeyBase64))
            {
                sig = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            return $"{Base64Url(payload)}.{Base64Url(sig)}";
        }

        /// <summary>
        /// 签发并保存为 .lic 文件
        /// </summary>
        public static void CreateFile(LicenseInfo info, string privateKeyBase64, string licPath)
        {
            string? dir = Path.GetDirectoryName(licPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(licPath, Create(info, privateKeyBase64), Encoding.ASCII);
        }

        /// <summary>
        /// 创建试用版 License（绑定本机 + N 天有效期）
        /// </summary>
        public static string CreateTrial(string privateKeyBase64, string customer, int days,
            string[]? features = null)
        {
            var info = new LicenseInfo
            {
                Customer = customer,
                MachineCode = MachineCodeEx.GetMachineCode(),
                IssuedAtUtc = DateTime.UtcNow,
                ExpireAtUtc = DateTime.UtcNow.AddDays(days),
            };
            if (features != null) info.Features.AddRange(features);
            info.Extra["Trial"] = "true";
            return Create(info, privateKeyBase64);
        }

        /// <summary>
        /// 校验 License 字符串
        /// </summary>
        /// <param name="licenseText">lic 文件内容</param>
        /// <param name="publicKeyBase64">嵌入客户端的公钥</param>
        /// <param name="checkMachine">是否校验机器码绑定</param>
        /// <param name="requiredFeature">要求的功能点（null = 不检查）</param>
        /// <param name="clockSkew">过期时间容差（防止临界抖动，默认 5 分钟）</param>
        public static LicenseVerifyResult Verify(
            string licenseText,
            string publicKeyBase64,
            bool checkMachine = true,
            string? requiredFeature = null,
            TimeSpan? clockSkew = null)
        {
            if (string.IsNullOrWhiteSpace(licenseText))
                return Fail(LicenseStatus.Malformed, "License 为空。");

            string[] parts = licenseText.Trim().Split('.');
            if (parts.Length != 2)
                return Fail(LicenseStatus.Malformed, "License 格式错误（应为 payload.signature）。");

            byte[] payload, sig;
            try
            {
                payload = FromBase64Url(parts[0]);
                sig = FromBase64Url(parts[1]);
            }
            catch (Exception ex) when (ex is FormatException
                                          or ArgumentException
                                          or InvalidOperationException
                                          or EncoderFallbackException)
            {
                // License 文本不是合法 Base64Url：按"格式错误"拒绝，这是预期内的输入错误，不向外抛。
                // 只吞这一类异常，其他（如空引用）说明代码有缺陷，必须暴露。
                Debug.WriteLine($"[LicenseManagerEx] License Base64 解码失败：{ex.Message}");
                return Fail(LicenseStatus.Malformed, "License Base64 解码失败。");
            }

            if (!payload.RsaVerify(sig, publicKeyBase64))
                return Fail(LicenseStatus.InvalidSignature, "签名无效：License 被篡改或签发方不匹配。");

            LicenseInfo? info;
            try
            {
                info = JsonSerializer.Deserialize<LicenseInfo>(Encoding.UTF8.GetString(payload), JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException
                                          or NotSupportedException
                                          or ArgumentException)
            {
                Debug.WriteLine($"[LicenseManagerEx] License 内容反序列化失败：{ex.Message}");
                return Fail(LicenseStatus.Malformed, "License 内容反序列化失败。");
            }
            if (info == null)
                return Fail(LicenseStatus.Malformed, "License 内容为空。");

            // ⛔ 序 32（2026-09-18 修）：`Features` / `Extra` 的**属性初始化器 `= new()` 只在 JSON 缺字段时生效**。
            // 厂商签发工具若显式序列化出 `"Features":null` / `"Extra":null`，反序列化会把 null 直接覆盖上来，
            // 于是下面 `info.Features.Count` 抛 NullReferenceException —— **签名完全合法的 License 也能炸**；
            // 而 NRE 不在本方法任何 catch 名单里 ⇒ 逃到调用方（拒绝服务，客户端无从防御）。
            // ⇒ 在反序列化边界做归一化：null 补成空集合，语义与"缺字段"一致（空 = 全部功能）。
            info.Features ??= new();
            info.Extra ??= new();

            DateTime now = DateTime.UtcNow + (clockSkew ?? TimeSpan.FromMinutes(5));
            if (now > info.ExpireAtUtc)
                return Fail(LicenseStatus.Expired, $"License 已过期（过期时间 {info.ExpireAtUtc:yyyy-MM-dd} UTC）。", info);

            if (checkMachine && !string.IsNullOrWhiteSpace(info.MachineCode) &&
                !MachineCodeEx.IsCurrentMachine(info.MachineCode))
                return Fail(LicenseStatus.MachineMismatch,
                    $"机器码不匹配：本机 {MachineCodeEx.GetMachineCode()}，License 绑定 {info.MachineCode}。", info);

            if (!string.IsNullOrEmpty(requiredFeature) &&
                info.Features.Count > 0 &&
                !info.Features.Contains(requiredFeature, StringComparer.OrdinalIgnoreCase))
                return Fail(LicenseStatus.FeatureDenied, $"缺少功能点：{requiredFeature}。", info);

            int daysLeft = info.IsPermanent ? int.MaxValue : Math.Max(0, (int)(info.ExpireAtUtc - DateTime.UtcNow).TotalDays);
            string msg = info.IsPermanent ? "License 有效（永久）。" : $"License 有效，剩余约 {daysLeft} 天。";
            return new LicenseVerifyResult { Status = LicenseStatus.Valid, License = info, Message = msg };
        }

        /// <summary>
        /// 校验 .lic 文件
        /// </summary>
        public static LicenseVerifyResult VerifyFile(
            string licPath, string publicKeyBase64,
            bool checkMachine = true, string? requiredFeature = null, TimeSpan? clockSkew = null)
        {
            if (!File.Exists(licPath))
                return Fail(LicenseStatus.Malformed, $"找不到 License 文件：{licPath}");
            return Verify(File.ReadAllText(licPath, Encoding.ASCII), publicKeyBase64, checkMachine, requiredFeature, clockSkew);
        }

        private static LicenseVerifyResult Fail(LicenseStatus s, string msg, LicenseInfo? lic = null)
            => new() { Status = s, Message = msg, License = lic };

        private static string Base64Url(byte[] data)
            => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string s)
        {
            string t = s.Replace('-', '+').Replace('_', '/');
            switch (t.Length % 4)
            {
                case 2: t += "=="; break;
                case 3: t += "="; break;
            }
            return Convert.FromBase64String(t);
        }
    }
}
