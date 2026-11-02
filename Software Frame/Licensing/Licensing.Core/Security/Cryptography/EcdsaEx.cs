// 【中文阅读说明】
// 本文件定义 EcdsaEx：ECDSA P-256 + SHA256 原语，供信任清单（SFM1）与新一代授权（SF3）使用。
// RsaEx 保留，继续服务历史 SF2 授权，本类不替换它。
// 选 ECDSA 而非 RSA-3072 的直接原因是体积：P-256 公钥固定 91 字节 / 122 个 Base64 字符，
// 人工可核对；RSA-3072 公钥约 528 字符，离线抄录必然出错。签名也从 384 字节降到 64 字节。
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Cryptography
{
    /// <summary>ECDSA P-256 签名与公钥规范化。</summary>
    public static class EcdsaEx
    {
        /// <summary>P-256 SPKI DER 的固定长度，用于在导入边界挡掉曲线不符与尾随数据。</summary>
        private const int PublicDerBytes = 91;

        /// <summary>生成密钥对：Base64(PKCS#8 私钥)、Base64(SPKI 公钥)。</summary>
        public static (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair()
        {
            using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return (
                Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey()),
                Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()));
        }

        public static ECDsa FromPrivateKey(string privateKeyBase64)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
            return ecdsa;
        }

        public static ECDsa FromPublicKey(string publicKeyBase64)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return ecdsa;
        }

        /// <summary>由私钥导出公钥，用于出厂前拦截「签名密钥与声明不符」。</summary>
        public static string PublicKeyFromPrivateKey(string privateKeyBase64)
        {
            using ECDsa ecdsa = FromPrivateKey(privateKeyBase64);
            return Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        }

        /// <summary>
        /// 严格规范化公钥：必须是无尾随数据的 P-256 SPKI DER，否则返回 null。
        /// 清单里的公钥直接决定「谁能签发授权」，这里放宽就是留后门
        /// （例如把 RSA 公钥塞进本该放 EC 公钥的字段）。
        /// </summary>
        public static string? NormalizePublicKey(string? publicKeyBase64)
        {
            if (string.IsNullOrWhiteSpace(publicKeyBase64)) return null;
            try
            {
                byte[] der = Convert.FromBase64String(publicKeyBase64.Trim());
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(der, out int consumed);
                if (consumed != der.Length || der.Length != PublicDerBytes) return null;

                byte[] canonical = ecdsa.ExportSubjectPublicKeyInfo();
                return canonical.Length == PublicDerBytes ? Convert.ToBase64String(canonical) : null;
            }
            catch (Exception ex) when (ex is CryptographicException
                                          or FormatException
                                          or ArgumentException)
            {
                Debug.WriteLine($"[EcdsaEx] 公钥不合法（按不可用处理）：{ex.Message}");
                return null;
            }
        }

        /// <summary>公钥指纹 = SHA256(SPKI DER)，大写十六进制。</summary>
        public static string? Fingerprint(string publicKeyBase64)
        {
            string? canonical = NormalizePublicKey(publicKeyBase64);
            if (canonical == null) return null;
            return Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(canonical)));
        }

        /// <summary>对 ASCII 文本签名，返回 Base64（默认 ASN.1 DER 编码；签验两侧必须同 API）。</summary>
        public static string Sign(string text, string privateKeyBase64)
        {
            using ECDsa ecdsa = FromPrivateKey(privateKeyBase64);
            byte[] signature = ecdsa.SignData(Encoding.ASCII.GetBytes(text), HashAlgorithmName.SHA256);
            return Convert.ToBase64String(signature);
        }

        /// <summary>验签；任何异常按「无效」处理，保持与 RsaEx.RsaVerify 一致的 fail-closed 语义。</summary>
        public static bool Verify(string text, string signatureBase64, string publicKeyBase64)
        {
            try
            {
                using ECDsa ecdsa = FromPublicKey(publicKeyBase64);
                return ecdsa.VerifyData(Encoding.ASCII.GetBytes(text),
                    Convert.FromBase64String(signatureBase64), HashAlgorithmName.SHA256);
            }
            catch (Exception ex) when (ex is CryptographicException
                                          or FormatException
                                          or ArgumentException
                                          or ArgumentNullException)
            {
                Debug.WriteLine($"[EcdsaEx] 验签失败（按无效处理）：{ex.Message}");
                return false;
            }
        }
    }
}
