param(
    [Parameter(Mandatory = $true)][string]$PublicKeyFile,
    [Parameter(Mandatory = $true)][ValidatePattern('\A[0-9A-Fa-f]{64}\z')][string]$ExpectedFingerprint
)
$ErrorActionPreference = 'Stop'
# ASCII-only script for Windows PowerShell 5.1 and PowerShell 7 compatibility.
# Accept only a PKCS#1 public key containing exactly modulus and exponent.
# A private key, PEM, PKCS#8 and trailing data MUST be rejected before embedding.
# Format alone is not enough: the embedded key must also be the key pinned in Build\Trust.props,
# otherwise any well-formed key (including one the customer generated) would pass the build.
if (-not ('LicensingPublicKeyValidator' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Security.Cryptography;
public static class LicensingPublicKeyValidator
{
    private static int ReadLength(BinaryReader reader)
    {
        int first = reader.ReadByte();
        if (first < 128) return first;
        int count = first & 127;
        if (count < 1 || count > 4) throw new InvalidDataException("Invalid DER length.");
        int result = 0;
        for (int i = 0; i < count; i++)
        {
            int next = reader.ReadByte();
            if (i == 0 && next == 0) throw new InvalidDataException("Noncanonical DER length.");
            result = checked(result * 256 + next);
        }
        if (result < 128) throw new InvalidDataException("Noncanonical DER length.");
        return result;
    }
    private static byte[] ReadInteger(BinaryReader reader)
    {
        if (reader.ReadByte() != 2) throw new InvalidDataException("Expected RSA integer.");
        int length = ReadLength(reader);
        if (length < 1 || length > 2048) throw new InvalidDataException("Invalid RSA integer size.");
        byte[] value = reader.ReadBytes(length);
        if (value.Length != length || (value[0] & 128) != 0)
            throw new InvalidDataException("Invalid RSA integer.");
        if (value[0] == 0)
        {
            if (length == 1 || (value[1] & 128) == 0)
                throw new InvalidDataException("Noncanonical RSA integer.");
            byte[] unsigned = new byte[length - 1];
            Buffer.BlockCopy(value, 1, unsigned, 0, unsigned.Length);
            return unsigned;
        }
        return value;
    }
    public static byte[] Validate(string path)
    {
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Public key file is too large.");
        byte[] der = Convert.FromBase64String(File.ReadAllText(path).Trim());
        using (var memory = new MemoryStream(der))
        using (var reader = new BinaryReader(memory))
        {
            if (reader.ReadByte() != 48) throw new InvalidDataException("Expected RSA public key sequence.");
            int length = ReadLength(reader);
            if (length != memory.Length - memory.Position) throw new InvalidDataException("Trailing or truncated key data.");
            var parameters = new RSAParameters { Modulus = ReadInteger(reader), Exponent = ReadInteger(reader) };
            if (memory.Position != memory.Length) throw new InvalidDataException("Not a public-only RSA key.");
            if (parameters.Modulus.Length < 256 || (parameters.Modulus.Length == 256 && parameters.Modulus[0] < 128))
                throw new InvalidDataException("RSA key must be at least 2048 bits.");
            using (var rsa = RSA.Create()) { rsa.ImportParameters(parameters); }
        }
        // The strict parser above already proved the encoding is canonical, definite-length PKCS#1 with no
        // trailing bytes, so this same DER is what the issuer tool hashed when it wrote the file.
        // (ExportRSAPublicKey is unavailable when Windows PowerShell compiles this with .NET Framework.)
        return der;
    }
}
'@
}
$path = (Resolve-Path -LiteralPath $PublicKeyFile).Path
$canonical = [LicensingPublicKeyValidator]::Validate($path)
$actual = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($canonical)).Replace('-', '')
if ($actual -ne $ExpectedFingerprint.ToUpperInvariant()) {
    throw "Public key fingerprint mismatch: candidate=$actual pinned=$ExpectedFingerprint. The key is well formed but is not the issuer pinned in Build\Trust.props - commit the new Build\Keys\public-<fingerprint>.key and update that file, or override LicenseExpectedPublicKeyFingerprint in .local\client.props for a throwaway local identity."
}
Write-Output "Public-only RSA key validated. Fingerprint=$actual"
