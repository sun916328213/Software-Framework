using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Cryptography
{
    /// <summary>
    /// RSA 非对称加解密 / 签名验签扩展（2048bit，SHA256 + PKCS#1 v1.5）
    /// 私钥仅保存在授权签发端，客户端只嵌入公钥。
    /// </summary>
    public static class RsaEx
    {
        /// <summary>
        /// 生成 RSA 密钥对（返回 Base64(DER) 格式的私钥/公钥，方便存文本配置）
        /// </summary>
        public static (string PrivateKeyBase64, string PublicKeyBase64) GenerateKeyPair(int keySize = 2048)
        {
            using var rsa = RSA.Create(keySize);
            return (
                Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
                Convert.ToBase64String(rsa.ExportRSAPublicKey()));
        }

        /// <summary>
        /// 从 Base64(DER) 私钥导入 RSA
        /// </summary>
        public static RSA FromPrivateKey(string privateKeyBase64)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKeyBase64), out _);
            return rsa;
        }

        /// <summary>
        /// 从 Base64(DER) 公钥导入 RSA
        /// </summary>
        public static RSA FromPublicKey(string publicKeyBase64)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
            return rsa;
        }

        /// <summary>
        /// RSA 公钥加密（OAEP-SHA256，明文长度受密钥尺寸限制，2048bit 最多约 190 字节）
        /// </summary>
        public static string RsaEncrypt(this string plainText, string publicKeyBase64)
        {
            using var rsa = FromPublicKey(publicKeyBase64);
            byte[] cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText ?? string.Empty), RSAEncryptionPadding.OaepSHA256);
            return Convert.ToBase64String(cipher);
        }

        /// <summary>
        /// RSA 私钥解密
        /// </summary>
        public static string RsaDecrypt(this string cipherBase64, string privateKeyBase64)
        {
            using var rsa = FromPrivateKey(privateKeyBase64);
            byte[] plain = rsa.Decrypt(Convert.FromBase64String(cipherBase64), RSAEncryptionPadding.OaepSHA256);
            return Encoding.UTF8.GetString(plain);
        }

        /// <summary>
        /// RSA 私钥签名（SHA256 + PKCS#1 v1.5，返回 Base64）
        /// </summary>
        public static string RsaSign(this string data, string privateKeyBase64, Encoding? encoding = null)
        {
            using var rsa = FromPrivateKey(privateKeyBase64);
            byte[] sig = rsa.SignData((encoding ?? Encoding.UTF8).GetBytes(data ?? string.Empty),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sig);
        }

        /// <summary>
        /// RSA 公钥验签
        /// </summary>
        public static bool RsaVerify(this string data, string signatureBase64, string publicKeyBase64, Encoding? encoding = null)
        {
            try
            {
                using var rsa = FromPublicKey(publicKeyBase64);
                return rsa.VerifyData((encoding ?? Encoding.UTF8).GetBytes(data ?? string.Empty),
                    Convert.FromBase64String(signatureBase64),
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex) when (ex is CryptographicException
                                          or FormatException
                                          or ArgumentException
                                          or ArgumentNullException)
            {
                // 验签失败一律按"无效"处理（fail-closed，安全上正确），这个语义保留。
                // 但只吞"密钥/格式/参数"这一类异常，且必须留痕：
                // 否则公钥配错时会表现为"所有签名都无效"，完全查不出是配置问题。
                Debug.WriteLine($"[RsaEx] 验签失败（按无效处理）：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// RSA 公钥验签（字节数组版本，常量时间比较由底层保证）
        /// </summary>
        public static bool RsaVerify(this byte[] data, byte[] signature, string publicKeyBase64)
        {
            try
            {
                using var rsa = FromPublicKey(publicKeyBase64);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch (Exception ex) when (ex is CryptographicException
                                          or ArgumentException
                                          or ArgumentNullException)
            {
                Debug.WriteLine($"[RsaEx] 验签失败（字节数组版本，按无效处理）：{ex.Message}");
                return false;
            }
        }
    }
}
