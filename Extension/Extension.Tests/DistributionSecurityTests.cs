using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Extension.Security.Cryptography;
using Extension.Security.Licensing;
using Xunit;

namespace Extension.Tests;

public class DistributionSecurityTests
{
	[Fact]
	public void HashEx_Sha256Hex_已知向量()
	{
		Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "abc".Sha256Hex());
	}

	[Fact]
	public void HashEx_Sha256Hex_对null返回空串而不是抛异常()
	{
		Assert.Equal(string.Empty, ((string)null).Sha256Hex(null));
		Assert.Equal(string.Empty, ((byte[])null).Sha256Hex());
		Assert.Equal(string.Empty, ((string)null).Sha512Hex());
	}

	[Fact]
	public void HashEx_字符串版与字节版对同一UTF8内容结果一致()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("中文abc");
		Assert.Equal(bytes.Sha256Hex(), "中文abc".Sha256Hex());
		Assert.NotEqual("中文abc".Sha256Hex(Encoding.ASCII), "中文abc".Sha256Hex(Encoding.UTF8));
	}

	[Fact]
	public void HashEx_Sha512Hex_长度128且小写()
	{
		string text = "abc".Sha512Hex();
		Assert.Equal(128, text.Length);
		Assert.Matches("^[0-9a-f]{128}$", text);
	}

	[Fact]
	public void HashEx_Sha256File_与内存哈希一致_不存在的文件抛FileNotFound()
	{
		TempWorkspace ws = new TempWorkspace("hash");
		try
		{
			string path = ws.WriteFile("a.txt", "abc");
			Assert.Equal("abc".Sha256Hex(), HashEx.Sha256File(path));
			Assert.Throws<FileNotFoundException>(() => HashEx.Sha256File(ws.Combine("nope.txt")));
		}
		finally
		{
			if (ws != null)
			{
				((IDisposable)ws).Dispose();
			}
		}
	}

	[Fact]
	public void HashEx_HmacSha256_RFC4231测试向量()
	{
		string text = new string('\v', 20);
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		Assert.Equal("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7", HashEx.HmacSha256Hex("Hi There", text));
		Assert.Equal(Convert.ToBase64String(Convert.FromHexString("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7")), HashEx.HmacSha256Base64("Hi There", bytes));
	}

	[Fact]
	public void HashEx_Pbkdf2_长度可控且同输入稳定_换盐即换密钥()
	{
		byte[] bytes = Encoding.ASCII.GetBytes("12345678");
		byte[] array = HashEx.Pbkdf2("pw", bytes, 32, 1000);
		byte[] actual = HashEx.Pbkdf2("pw", bytes, 32, 1000);
		Assert.Equal(32, array.Length);
		Assert.Equal(array, actual);
		Assert.Equal(16, HashEx.Pbkdf2("pw", bytes, 16, 1000).Length);
		Assert.NotEqual(array, HashEx.Pbkdf2("pw", Encoding.ASCII.GetBytes("87654321"), 32, 1000));
		Assert.NotEqual(array, HashEx.Pbkdf2("pw2", bytes, 32, 1000));
		Assert.NotEqual(array, HashEx.Pbkdf2("pw", bytes, 32, 1001));
	}

	[Fact]
	public void HashEx_SecureRandom_长度正确且两次不同()
	{
		byte[] array = HashEx.SecureRandom(32);
		byte[] array2 = HashEx.SecureRandom(32);
		Assert.Equal(32, array.Length);
		Assert.Equal(32, array2.Length);
		Assert.NotEqual(array, array2);
		Assert.Empty(HashEx.SecureRandom(0));
		Assert.Throws<OverflowException>(() => HashEx.SecureRandom(-1));
	}

	[Fact]
	public void HashEx_FixedTimeEquals_任意一侧为null都返回false()
	{
		byte[] array = new byte[3] { 1, 2, 3 };
		Assert.True(HashEx.FixedTimeEquals(array, array));
		Assert.True(HashEx.FixedTimeEquals(array, new byte[3] { 1, 2, 3 }));
		Assert.False(HashEx.FixedTimeEquals(array, new byte[3] { 1, 2, 4 }));
		Assert.False(HashEx.FixedTimeEquals(array, new byte[2] { 1, 2 }));
		Assert.False(HashEx.FixedTimeEquals(null, null));
		Assert.False(HashEx.FixedTimeEquals(array, null));
		Assert.False(HashEx.FixedTimeEquals(null, array));
	}

	[Fact]
	public void AesEx_CBC字符串往返_且每次密文都不同()
	{
		string text = "机密内容".AesEncrypt("pw");
		string actual = "机密内容".AesEncrypt("pw");
		Assert.Equal("机密内容", text.AesDecrypt("pw"));
		Assert.NotEqual(text, actual);
	}

	[Fact]
	public void AesEx_CBC口令错误_绝不会返回原文()
	{
		string cipherBase = "机密内容".AesEncrypt("pw");
		try
		{
			Assert.NotEqual("机密内容", cipherBase.AesDecrypt("pw2"));
		}
		catch (Exception ex)
		{
			Assert.IsType<CryptographicException>(ex);
		}
	}

	[Fact]
	public void AesEx_CBC_明文为null时返回空串_而空串是解不开的()
	{
		Assert.Equal(string.Empty, ((string)null).AesEncrypt("pw"));
		Assert.Throws<CryptographicException>(() => string.Empty.AesDecrypt("pw"));
	}

	[Fact]
	public void AesEx_CBC_空串能往返()
	{
		Assert.Equal(string.Empty, string.Empty.AesEncrypt("pw").AesDecrypt("pw"));
	}

	[Fact]
	public void AesEx_CBC_密文格式非法_抛CryptographicException()
	{
		Assert.Throws<CryptographicException>(() => Convert.ToBase64String(new byte[10]).AesDecrypt("pw"));
		Assert.Throws<FormatException>(() => "!!!".AesDecrypt("pw"));
	}

	[Fact]
	public void AesEx_CBC没有完整性保护_改一个字节不抛但明文变了()
	{
		string s = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA".AesEncrypt("pw");
		byte[] array = Convert.FromBase64String(s);
		Assert.Equal(80, array.Length);
		array[16] ^= 1;
		string cipherBase = Convert.ToBase64String(array);
		string actual = cipherBase.AesDecrypt("pw");
		Assert.NotEqual("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", actual);
	}

	[Fact]
	public void AesEx_GCM字符串往返_且每次密文都不同()
	{
		byte[] key = HashEx.SecureRandom(32);
		string text = "机密内容".AesGcmEncrypt(key);
		string actual = "机密内容".AesGcmEncrypt(key);
		Assert.Equal("机密内容", text.AesGcmDecrypt(key));
		Assert.NotEqual(text, actual);
	}

	[Fact]
	public void AesEx_GCM密钥必须32字节_否则抛ArgumentException()
	{
		Assert.Throws<ArgumentException>(() => "x".AesGcmEncrypt(null));
		Assert.Throws<ArgumentException>(() => "x".AesGcmEncrypt(new byte[16]));
		Assert.Throws<ArgumentException>(() => "x".AesGcmEncrypt(new byte[33]));
		Assert.Throws<ArgumentException>(() => "AAAA".AesGcmDecrypt(new byte[16]));
		Assert.Throws<ArgumentException>(() => "AAAA".AesGcmDecrypt(null));
	}

	[Fact]
	public void AesEx_GCM_密文或tag被改_抛AuthenticationTagMismatchException()
	{
		byte[] key = HashEx.SecureRandom(32);
		string cipher = "机密内容".AesGcmEncrypt(key);
		byte[] flipCipher = Convert.FromBase64String(cipher);
		flipCipher[^1] ^= 1;
		Assert.ThrowsAny<CryptographicException>(() => Convert.ToBase64String(flipCipher).AesGcmDecrypt(key));
		byte[] flipTag = Convert.FromBase64String(cipher);
		flipTag[12] ^= 1;
		Assert.ThrowsAny<CryptographicException>(() => Convert.ToBase64String(flipTag).AesGcmDecrypt(key));
		Assert.ThrowsAny<CryptographicException>(() => cipher.AesGcmDecrypt(HashEx.SecureRandom(32)));
	}

	[Fact]
	public void AesEx_GCM_空明文能加密_但解不开_往返断裂()
	{
		byte[] key = HashEx.SecureRandom(32);
		string cipher = string.Empty.AesGcmEncrypt(key);
		Assert.Equal(28, Convert.FromBase64String(cipher).Length);
		Assert.Throws<CryptographicException>(() => cipher.AesGcmDecrypt(key));
	}

	[Fact]
	public void AesEx_GCM_密文格式非法_抛CryptographicException()
	{
		byte[] key = HashEx.SecureRandom(32);
		Assert.Throws<CryptographicException>(() => Convert.ToBase64String(new byte[10]).AesGcmDecrypt(key));
		Assert.Throws<FormatException>(() => "!!!".AesGcmDecrypt(key));
	}

	[Fact]
	public void AesEx_文件加解密往返_密文与明文不同()
	{
		using TempWorkspace tempWorkspace = new TempWorkspace("aes");
		string text = tempWorkspace.WriteFile("plain.bin", "hello 世界");
		string text2 = tempWorkspace.Combine("enc.bin");
		string text3 = tempWorkspace.Combine("dec.bin");
		AesEx.AesEncryptFile(text, text2, "pw");
		Assert.NotEqual(File.ReadAllBytes(text), File.ReadAllBytes(text2));
		Assert.NotEqual(File.ReadAllText(text), File.ReadAllText(text2));
		AesEx.AesDecryptFile(text2, text3, "pw");
		Assert.Equal(File.ReadAllText(text), File.ReadAllText(text3));
	}

	[Fact]
	public void AesEx_文件头不足32字节_解密抛CryptographicException()
	{
		TempWorkspace ws = new TempWorkspace("aes_bad");
		try
		{
			string bad = ws.WriteFile("bad.bin", "short");
			Assert.Throws<CryptographicException>(delegate
			{
				AesEx.AesDecryptFile(bad, ws.Combine("out.bin"), "pw");
			});
		}
		finally
		{
			if (ws != null)
			{
				((IDisposable)ws).Dispose();
			}
		}
	}

	[Fact]
	public void RsaEx_密钥对生成_2048位且可往返导入()
	{
		var (privateKeyBase, publicKeyBase) = RsaEx.GenerateKeyPair();
		using RSA rSA = RsaEx.FromPrivateKey(privateKeyBase);
		using RSA rSA2 = RsaEx.FromPublicKey(publicKeyBase);
		Assert.Equal(2048, rSA.KeySize);
		Assert.Equal(2048, rSA2.KeySize);
		Assert.Equal(3072, RsaEx.FromPublicKey(RsaEx.GenerateKeyPair(3072).PublicKeyBase64).KeySize);
	}

	[Fact]
	public void RsaEx_签名验签往返_篡改后失败_且PKCS1签名是确定性的()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string text = "hello".RsaSign(item);
		Assert.True("hello".RsaVerify(text, item2));
		Assert.False("hello!".RsaVerify(text, item2));
		Assert.Equal(text, "hello".RsaSign(item));
	}

	[Fact]
	public void RsaEx_null明文被静默当成空串签名()
	{
		var (privateKeyBase, publicKeyBase) = RsaEx.GenerateKeyPair();
		Assert.Equal(string.Empty.RsaSign(privateKeyBase), ((string)null).RsaSign(privateKeyBase));
		Assert.True(((string)null).RsaVerify(string.Empty.RsaSign(privateKeyBase), publicKeyBase));
	}

	[Fact]
	public void RsaEx_验签失败一律返回false不抛_符合fail_closed()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string signatureBase = "x".RsaSign(item);
		Assert.False("x".RsaVerify("not-base64!!", item2));
		Assert.False("x".RsaVerify(signatureBase, "not-base64!!"));
		Assert.False("x".RsaVerify(signatureBase, null));
		Assert.False("x".RsaVerify(null, item2));
		Assert.False("x".RsaVerify(signatureBase, string.Empty));
		Assert.False(new byte[1] { 1 }.RsaVerify(new byte[1] { 2 }, item2));
		Assert.False(new byte[1] { 1 }.RsaVerify(new byte[1] { 2 }, null));
	}

	[Fact]
	public void RsaEx_加密解密往返_且超过OAEP上限时抛异常而不是静默截断()
	{
		var (privateKeyBase, pub) = RsaEx.GenerateKeyPair();
		Assert.Equal("机密", "机密".RsaEncrypt(pub).RsaDecrypt(privateKeyBase));
		Assert.Equal(string.Empty, ((string)null).RsaEncrypt(pub).RsaDecrypt(privateKeyBase));
		string text = new string('a', 190);
		Assert.Equal(text, text.RsaEncrypt(pub).RsaDecrypt(privateKeyBase));
		Assert.Throws<CryptographicException>(() => new string('a', 191).RsaEncrypt(pub));
	}

	[Fact]
	public void RsaEx_解密失败是抛异常_与验签的fail_closed风格不同()
	{
		string item = RsaEx.GenerateKeyPair().PublicKeyBase64;
		string priv2 = RsaEx.GenerateKeyPair().PrivateKeyBase64;
		string cipher = "x".RsaEncrypt(item);
		Assert.Throws<CryptographicException>(() => cipher.RsaDecrypt(priv2));
		Assert.Throws<FormatException>(() => "not-base64".RsaDecrypt(priv2));
	}

	[Fact]
	public void RsaEx_只认PKCS1格式的DER_不认PKCS8和SPKI()
	{
		var (privateKeyBase, publicKeyBase) = RsaEx.GenerateKeyPair();
		string pkcs8;
		using (RSA rSA = RsaEx.FromPrivateKey(privateKeyBase))
		{
			pkcs8 = Convert.ToBase64String(rSA.ExportPkcs8PrivateKey());
		}
		string spki;
		using (RSA rSA2 = RsaEx.FromPublicKey(publicKeyBase))
		{
			spki = Convert.ToBase64String(rSA2.ExportSubjectPublicKeyInfo());
		}
		Assert.Throws<CryptographicException>(() => RsaEx.FromPrivateKey(pkcs8));
		Assert.Throws<CryptographicException>(() => RsaEx.FromPublicKey(spki));
	}

	[Fact]
	public void RsaEx_导入非Base64密钥_抛FormatException或ArgumentNullException()
	{
		Assert.Throws<FormatException>(() => RsaEx.FromPrivateKey("not-base64!!"));
		Assert.Throws<FormatException>(() => RsaEx.FromPublicKey("not-base64!!"));
		Assert.Throws<ArgumentNullException>(() => RsaEx.FromPrivateKey(null));
		Assert.Throws<ArgumentNullException>(() => RsaEx.FromPublicKey(null));
	}

	[Fact]
	public void LicenseInfo_默认值_永久且不绑定机器()
	{
		LicenseInfo licenseInfo = new LicenseInfo();
		Assert.Equal(32, licenseInfo.Id.Length);
		Assert.Matches("^[0-9a-f]{32}$", licenseInfo.Id);
		Assert.Equal(string.Empty, licenseInfo.Customer);
		Assert.Equal(string.Empty, licenseInfo.MachineCode);
		Assert.Equal(DateTime.MaxValue, licenseInfo.ExpireAtUtc);
		Assert.NotNull(licenseInfo.Features);
		Assert.Empty(licenseInfo.Features);
		Assert.NotNull(licenseInfo.Extra);
		Assert.Empty(licenseInfo.Extra);
		Assert.True(licenseInfo.IsPermanent);
		Assert.True((DateTime.UtcNow - licenseInfo.IssuedAtUtc).Duration() < TimeSpan.FromMinutes(1L));
	}

	[Fact]
	public void LicenseInfo_IsPermanent的判据是距MaxValue不超过一天_比文档宽松()
	{
		Assert.True(new LicenseInfo
		{
			ExpireAtUtc = DateTime.MaxValue
		}.IsPermanent);
		Assert.True(new LicenseInfo
		{
			ExpireAtUtc = DateTime.MaxValue.AddDays(-1.0)
		}.IsPermanent);
		Assert.False(new LicenseInfo
		{
			ExpireAtUtc = DateTime.MaxValue.AddDays(-2.0)
		}.IsPermanent);
		Assert.False(new LicenseInfo
		{
			ExpireAtUtc = DateTime.UtcNow.AddYears(100)
		}.IsPermanent);
	}

	[Fact]
	public void LicenseInfo_JSON里的显式null会覆盖属性初始化器()
	{
		LicenseInfo licenseInfo = JsonSerializer.Deserialize<LicenseInfo>("{\"Features\":null,\"Extra\":null}");
		Assert.NotNull(licenseInfo);
		Assert.Null(licenseInfo.Features);
		Assert.Null(licenseInfo.Extra);
		LicenseInfo licenseInfo2 = JsonSerializer.Deserialize<LicenseInfo>("{}");
		Assert.NotNull(licenseInfo2.Features);
		Assert.NotNull(licenseInfo2.Extra);
	}

	[Fact]
	public void LicenseManagerEx_签发与校验往返_永久License()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string licenseText = LicenseManagerEx.Create(new LicenseInfo
		{
			Customer = "ACME",
			MachineCode = "",
			Features = { "Pro" }
		}, item);
		LicenseVerifyResult licenseVerifyResult = LicenseManagerEx.Verify(licenseText, item2, checkMachine: false);
		Assert.Equal(LicenseStatus.Valid, licenseVerifyResult.Status);
		Assert.True(licenseVerifyResult.Ok);
		Assert.Equal("ACME", licenseVerifyResult.License.Customer);
		Assert.Equal("Pro", Assert.Single(licenseVerifyResult.License.Features));
		Assert.Contains("永久", licenseVerifyResult.Message);
	}

	[Fact]
	public void LicenseManagerEx_篡改payload后判签名无效()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string text = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = ""
		}, item);
		string[] array = text.Split('.');
		char c = ((array[0][0] == 'A') ? 'B' : 'A');
		string licenseText = c + array[0].Substring(1) + "." + array[1];
		Assert.Equal(LicenseStatus.InvalidSignature, LicenseManagerEx.Verify(licenseText, item2, checkMachine: false).Status);
	}

	[Fact]
	public void LicenseManagerEx_格式错误的各种输入都判Malformed()
	{
		string item = RsaEx.GenerateKeyPair().PublicKeyBase64;
		string[] array = new string[6] { "", "   ", "abc", "a.b.c", "!!!.???", "a." };
		foreach (string licenseText in array)
		{
			LicenseVerifyResult licenseVerifyResult = LicenseManagerEx.Verify(licenseText, item);
			Assert.Equal(LicenseStatus.Malformed, licenseVerifyResult.Status);
			Assert.False(licenseVerifyResult.Ok);
			Assert.NotEqual(string.Empty, licenseVerifyResult.Message);
		}
	}

	[Fact]
	public void LicenseManagerEx_空payload加空签名被判签名无效_而不是格式错误()
	{
		string item = RsaEx.GenerateKeyPair().PublicKeyBase64;
		Assert.Equal(LicenseStatus.InvalidSignature, LicenseManagerEx.Verify(".", item).Status);
	}

	[Fact]
	public void LicenseManagerEx_clockSkew是提前5分钟判过期_方向与文档的容差说法相反()
	{
		var (priv, pub) = RsaEx.GenerateKeyPair();
		Assert.Equal(LicenseStatus.Expired, St(Lic(DateTime.UtcNow.AddMinutes(4.0))));
		Assert.Equal(LicenseStatus.Valid, St(Lic(DateTime.UtcNow.AddMinutes(6.0))));
		Assert.Equal(LicenseStatus.Expired, St(Lic(DateTime.UtcNow.AddMinutes(-4.0))));
		Assert.Equal(LicenseStatus.Valid, St(Lic(DateTime.UtcNow.AddMinutes(4.0)), TimeSpan.Zero));
		string Lic(DateTime expire)
		{
			return LicenseManagerEx.Create(new LicenseInfo
			{
				MachineCode = "",
				ExpireAtUtc = expire
			}, priv);
		}
		LicenseStatus St(string lic, TimeSpan? skew = null)
		{
			return LicenseManagerEx.Verify(lic, pub, checkMachine: false, null, skew).Status;
		}
	}

	[Fact]
	public void LicenseManagerEx_机器码绑定_空机器码等于不绑定()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string licenseText = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = "AAAA-BBBB-CCCC-DDDD"
		}, item);
		Assert.Equal(LicenseStatus.MachineMismatch, LicenseManagerEx.Verify(licenseText, item2).Status);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText, item2, checkMachine: false).Status);
		string licenseText2 = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = ""
		}, item);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText2, item2).Status);
		string licenseText3 = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = MachineCodeEx.GetMachineCode()
		}, item);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText3, item2).Status);
	}

	[Fact]
	public void LicenseManagerEx_功能点为空列表时_任何功能都放行()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string licenseText = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = ""
		}, item);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText, item2, checkMachine: false, "任意功能").Status);
		string licenseText2 = LicenseManagerEx.Create(new LicenseInfo
		{
			MachineCode = "",
			Features = { "Pro" }
		}, item);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText2, item2, checkMachine: false, "pro").Status);
		Assert.Equal(LicenseStatus.FeatureDenied, LicenseManagerEx.Verify(licenseText2, item2, checkMachine: false, "Ultra").Status);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText2, item2, checkMachine: false).Status);
	}

	[Fact]
	public void LicenseManagerEx_签名合法但Features为null_修后归一化成空集合按全功能放行()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		LicenseInfo info = new LicenseInfo
		{
			Customer = "ACME",
			MachineCode = "",
			Features = null,
			Extra = null
		};
		string licenseText = LicenseManagerEx.Create(info, item);
		string actualString = JsonSerializer.Serialize(new LicenseInfo
		{
			MachineCode = "",
			Features = null,
			Extra = null
		});
		Assert.Contains("\"Features\":null", actualString);
		Assert.Contains("\"Extra\":null", actualString);
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.Verify(licenseText, item2, checkMachine: false).Status);
		LicenseVerifyResult licenseVerifyResult = LicenseManagerEx.Verify(licenseText, item2, checkMachine: false, "Pro");
		Assert.Equal(LicenseStatus.Valid, licenseVerifyResult.Status);
		Assert.True(licenseVerifyResult.Ok);
		Assert.NotNull(licenseVerifyResult.License.Features);
		Assert.Empty(licenseVerifyResult.License.Features);
		Assert.NotNull(licenseVerifyResult.License.Extra);
		Assert.Empty(licenseVerifyResult.License.Extra);
	}

	[Fact]
	public void LicenseManagerEx_CreateTrial_绑定本机并打Trial标记()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string licenseText = LicenseManagerEx.CreateTrial(item, "试用客户", 30, new string[1] { "Pro" });
		LicenseVerifyResult licenseVerifyResult = LicenseManagerEx.Verify(licenseText, item2, checkMachine: true, "Pro");
		Assert.Equal(LicenseStatus.Valid, licenseVerifyResult.Status);
		Assert.Equal("试用客户", licenseVerifyResult.License.Customer);
		Assert.Equal("true", licenseVerifyResult.License.Extra["Trial"]);
		Assert.Equal(MachineCodeEx.GetMachineCode(), licenseVerifyResult.License.MachineCode);
		Assert.False(licenseVerifyResult.License.IsPermanent);
		Assert.Contains("剩余约", licenseVerifyResult.Message);
	}

	[Fact]
	public void LicenseManagerEx_CreateTrial_不传功能点时Features保持为空()
	{
		(string PrivateKeyBase64, string PublicKeyBase64) tuple = RsaEx.GenerateKeyPair();
		string item = tuple.PrivateKeyBase64;
		string item2 = tuple.PublicKeyBase64;
		string licenseText = LicenseManagerEx.CreateTrial(item, "c", 1);
		LicenseVerifyResult licenseVerifyResult = LicenseManagerEx.Verify(licenseText, item2, checkMachine: false);
		Assert.Empty(licenseVerifyResult.License.Features);
	}

	[Fact]
	public void LicenseManagerEx_VerifyFile_文件不存在判Malformed_存在时往返成功()
	{
		var (privateKeyBase, publicKeyBase) = RsaEx.GenerateKeyPair();
		using TempWorkspace tempWorkspace = new TempWorkspace("lic");
		Assert.Equal(LicenseStatus.Malformed, LicenseManagerEx.VerifyFile(tempWorkspace.Combine("nope.lic"), publicKeyBase).Status);
		string text = tempWorkspace.Combine("a.lic");
		LicenseManagerEx.CreateFile(new LicenseInfo
		{
			MachineCode = ""
		}, privateKeyBase, text);
		Assert.True(File.Exists(text));
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.VerifyFile(text, publicKeyBase, checkMachine: false).Status);
	}

	[Fact]
	public void LicenseManagerEx_CreateFile_自动补出多层目录()
	{
		var (privateKeyBase, publicKeyBase) = RsaEx.GenerateKeyPair();
		using TempWorkspace tempWorkspace = new TempWorkspace("lic_dir");
		string text = tempWorkspace.Combine("sub", "deep", "a.lic");
		LicenseManagerEx.CreateFile(new LicenseInfo
		{
			MachineCode = ""
		}, privateKeyBase, text);
		Assert.True(File.Exists(text));
		Assert.Equal(LicenseStatus.Valid, LicenseManagerEx.VerifyFile(text, publicKeyBase, checkMachine: false).Status);
	}

	[Fact]
	public void MachineCodeEx_机器码格式固定_同机多次调用稳定()
	{
		string machineCode = MachineCodeEx.GetMachineCode();
		string machineCode2 = MachineCodeEx.GetMachineCode();
		Assert.Equal(machineCode, machineCode2);
		Assert.Equal(19, machineCode.Length);
		Assert.Matches("^[0-9A-F]{4}(-[0-9A-F]{4}){3}$", machineCode);
	}

	[Fact]
	public void MachineCodeEx_IsCurrentMachine_大小写不敏感_空白一律false()
	{
		string machineCode = MachineCodeEx.GetMachineCode();
		Assert.True(MachineCodeEx.IsCurrentMachine(machineCode));
		Assert.True(MachineCodeEx.IsCurrentMachine(machineCode.ToLowerInvariant()));
		Assert.True(MachineCodeEx.IsCurrentMachine("  " + machineCode + "  "));
		Assert.False(MachineCodeEx.IsCurrentMachine(null));
		Assert.False(MachineCodeEx.IsCurrentMachine(string.Empty));
		Assert.False(MachineCodeEx.IsCurrentMachine("   "));
		Assert.False(MachineCodeEx.IsCurrentMachine("0000-0000-0000-0000"));
	}

	[Fact]
	public void MachineCodeEx_采集失败时降级为占位符_但两处都必须留痕()
	{
		string text = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Licensing", "MachineCodeEx.cs"));
		Assert.Contains("\"NOMAC|\"", text);
		Assert.Contains("\"NOSERIAL\"", text);
		Assert.Equal(2, text.Split("Debug.WriteLine").Length - 1);
	}

	[Fact]
	public void MachineCodeEx_用PInvoke取卷序列号_守卫扫不到DllImport字符串_这不是漏网()
	{
		string actualString = File.ReadAllText(Path.Combine(ExtensionSourceRoot(), "Security", "Licensing", "MachineCodeEx.cs"));
		Assert.Contains("DllImport(\"kernel32.dll\"", actualString);
		Assert.Contains("GetVolumeInformation", actualString);
	}

	private static string ExtensionSourceRoot([CallerFilePath] string thisFile = "")
	{
		string directoryName = Path.GetDirectoryName(thisFile);
		Assert.False(string.IsNullOrEmpty(directoryName), "无法从 CallerFilePath 解析出测试目录");
		return Path.GetFullPath(Path.Combine(directoryName, "..", "Extension"));
	}
}
