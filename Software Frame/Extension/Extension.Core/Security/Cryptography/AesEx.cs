// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：AesEx。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.IO、System.Security.Cryptography、System.Text。导入不代表所有类型都会在运行时加载。
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Extension.Security.Cryptography
{
    /// <summary>
    /// AES 对称加解密扩展（口令派生 + CBC / GCM）
    /// 数据格式（CBC）：salt(16) + iv(16) + cipher，整体 Base64
    /// 数据格式（GCM）：nonce(12) + tag(16) + cipher，整体 Base64
    /// </summary>
    public static class AesEx
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int Iterations = 60000;

        /// <summary>
        /// AES-CBC 加密字符串（PBKDF2 从口令派生 256bit 密钥）
        /// </summary>
        public static string AesEncrypt(this string plainText, string password)
        {
            if (plainText == null) return string.Empty;
            byte[] salt = HashEx.SecureRandom(SaltSize);
            byte[] key = HashEx.Pbkdf2(password, salt, 32, Iterations);
            try
            {
                using var aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();
                using var ms = new MemoryStream();
                ms.Write(salt, 0, salt.Length);
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] plain = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(plain, 0, plain.Length);
                }
                return Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// AES-CBC 解密字符串
        /// </summary>
        public static string AesDecrypt(this string cipherBase64, string password)
        {
            byte[] all = Convert.FromBase64String(cipherBase64);
            if (all.Length < SaltSize + IvSize + 1)
                throw new CryptographicException("密文格式非法。");
            byte[] salt = all[0..SaltSize];
            byte[] iv = all[SaltSize..(SaltSize + IvSize)];
            byte[] cipher = all[(SaltSize + IvSize)..];
            byte[] key = HashEx.Pbkdf2(password, salt, 32, Iterations);
            try
            {
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(new MemoryStream(cipher), aes.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    cs.CopyTo(ms);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// AES-GCM 加密字符串（AEAD，防篡改，推荐新代码使用）
        /// </summary>
        public static string AesGcmEncrypt(this string plainText, byte[] key32)
        {
            if (key32 == null || key32.Length != 32)
                throw new ArgumentException("GCM 密钥必须为 32 字节。", nameof(key32));
            byte[] nonce = HashEx.SecureRandom(NonceSize);
            byte[] plain = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            byte[] cipher = new byte[plain.Length];
            byte[] tag = new byte[TagSize];
            using (var gcm = new AesGcm(key32, TagSize))
            {
                gcm.Encrypt(nonce, plain, cipher, tag);
            }
            byte[] all = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, all, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, all, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, all, NonceSize + TagSize, cipher.Length);
            return Convert.ToBase64String(all);
        }

        /// <summary>
        /// AES-GCM 解密字符串（tag 校验失败抛 CryptographicException，说明被篡改）
        /// </summary>
        public static string AesGcmDecrypt(this string cipherBase64, byte[] key32)
        {
            if (key32 == null || key32.Length != 32)
                throw new ArgumentException("GCM 密钥必须为 32 字节。", nameof(key32));
            byte[] all = Convert.FromBase64String(cipherBase64);
            if (all.Length < NonceSize + TagSize + 1)
                throw new CryptographicException("密文格式非法。");
            byte[] nonce = all[0..NonceSize];
            byte[] tag = all[NonceSize..(NonceSize + TagSize)];
            byte[] cipher = all[(NonceSize + TagSize)..];
            byte[] plain = new byte[cipher.Length];
            using (var gcm = new AesGcm(key32, TagSize))
            {
                gcm.Decrypt(nonce, cipher, tag, plain);
            }
            return Encoding.UTF8.GetString(plain);
        }

        /// <summary>
        /// AES-CBC 加密文件（流式，大文件友好）
        /// </summary>
        public static void AesEncryptFile(string srcPath, string dstPath, string password)
        {
            byte[] salt = HashEx.SecureRandom(SaltSize);
            byte[] key = HashEx.Pbkdf2(password, salt, 32, Iterations);
            try
            {
                using var aes = Aes.Create();
                aes.Key = key;
                aes.GenerateIV();
                using var fsOut = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None);
                fsOut.Write(salt, 0, salt.Length);
                fsOut.Write(aes.IV, 0, aes.IV.Length);
                using var cs = new CryptoStream(fsOut, aes.CreateEncryptor(), CryptoStreamMode.Write);
                using var fsIn = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                fsIn.CopyTo(cs);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// AES-CBC 解密文件
        /// </summary>
        public static void AesDecryptFile(string srcPath, string dstPath, string password)
        {
            using var fsIn = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            if (fsIn.Read(salt, 0, salt.Length) != SaltSize ||
                fsIn.Read(iv, 0, iv.Length) != IvSize)
                throw new CryptographicException("密文文件格式非法。");
            byte[] key = HashEx.Pbkdf2(password, salt, 32, Iterations);
            try
            {
                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                using var cs = new CryptoStream(fsIn, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var fsOut = new FileStream(dstPath, FileMode.Create, FileAccess.Write, FileShare.None);
                cs.CopyTo(fsOut);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
