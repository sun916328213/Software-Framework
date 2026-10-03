// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：HashEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Security.Cryptography、System.Text。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Cryptography
{
    /// <summary>
    /// 哈希与密钥派生扩展（SHA / HMAC / PBKDF2 / 安全随机数）
    /// </summary>
    public static class HashEx
    {
        /// <summary>
        /// 计算字符串的 SHA256（小写 hex）
        /// </summary>
        public static string Sha256Hex(this string data, Encoding? encoding = null)
        {
            if (data == null) return string.Empty;
            byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(data);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        /// <summary>
        /// 计算字节数组的 SHA256（小写 hex）
        /// </summary>
        public static string Sha256Hex(this byte[] data)
        {
            if (data == null) return string.Empty;
            return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        }

        /// <summary>
        /// 计算字符串的 SHA512（小写 hex）
        /// </summary>
        public static string Sha512Hex(this string data, Encoding? encoding = null)
        {
            if (data == null) return string.Empty;
            byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(data);
            return Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
        }

        /// <summary>
        /// 计算文件的 SHA256（小写 hex，流式读取，大文件友好）
        /// </summary>
        public static string Sha256File(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] hash = SHA256.HashData(fs);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// HMAC-SHA256（返回小写 hex）
        /// </summary>
        public static string HmacSha256Hex(string data, string key, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            using var hmac = new HMACSHA256(encoding.GetBytes(key));
            byte[] hash = hmac.ComputeHash(encoding.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// HMAC-SHA256（返回 Base64）
        /// </summary>
        public static string HmacSha256Base64(string data, byte[] key, Encoding? encoding = null)
        {
            using var hmac = new HMACSHA256(key);
            byte[] hash = hmac.ComputeHash((encoding ?? Encoding.UTF8).GetBytes(data));
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// PBKDF2 派生密钥（默认 SHA256）
        /// </summary>
        /// <param name="password">口令</param>
        /// <param name="salt">盐（至少 8 字节）</param>
        /// <param name="keyLength">派生密钥长度（字节）</param>
        /// <param name="iterations">迭代次数</param>
        public static byte[] Pbkdf2(string password, byte[] salt, int keyLength = 32, int iterations = 60000)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, keyLength);
        }

        /// <summary>
        /// 生成密码学安全随机字节
        /// </summary>
        public static byte[] SecureRandom(int length)
        {
            byte[] buf = new byte[length];
            RandomNumberGenerator.Fill(buf);
            return buf;
        }

        /// <summary>
        /// 常量时间比较（防时序攻击，用于比较哈希/签名）
        /// </summary>
        public static bool FixedTimeEquals(byte[]? a, byte[]? b)
        {
            if (a == null || b == null) return false;
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
