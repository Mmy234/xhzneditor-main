using System.Security.Cryptography;
using System.Text;

namespace Xhzn.PackFormat;

/// <summary>
/// .xhznpack 容器。编辑端实际写出的格式是权威来源，播放器必须能打开。
///
/// 编辑端当前格式：
///   0  : 4  MAGIC = ASCII "XHZN"
///   4  : 1  容器版本，现为 1
///   5  : 1  载荷类型，2 = 后面直接跟 zip
///   6  : N  标准 zip（PK\x03\x04…）
///
/// zip 内：
///   manifest.json / project.json / pages/{id}.json / assets/**
///
/// 另外仍能打开：
///   - 裸 zip（调试）
///   - 旧样例 AES 容器 MAGIC "XHZNPK01"（本仓库早期 golden 包）
/// </summary>
public static class PackCrypto
{
    public const string AesMagic = "XHZNPK01";
    public const int SupportedSchemaVersion = 1;
    public const byte ContainerVersion = 1;
    public const byte PayloadZip = 2;
    public const string AppSecret = "XHZN-EDITOR-PLAYER-PACK-KEY-V1";

    public static readonly byte[] EditorMagic = Encoding.ASCII.GetBytes("XHZN");
    public static readonly byte[] AesMagicBytes = Encoding.ASCII.GetBytes(AesMagic);

    public static byte[] EncryptZip(byte[] zipBytes) => WrapEditor(zipBytes);

    public static byte[] WrapEditor(byte[] zipBytes)
    {
        var output = new byte[6 + zipBytes.Length];
        Buffer.BlockCopy(EditorMagic, 0, output, 0, 4);
        output[4] = ContainerVersion;
        output[5] = PayloadZip;
        Buffer.BlockCopy(zipBytes, 0, output, 6, zipBytes.Length);
        return output;
    }

    public static byte[] DecryptToZip(byte[] packBytes)
    {
        if (packBytes.Length < 4)
            throw new InvalidDataException("发布包过短，不是有效的 .xhznpack。");

        if (IsZipHeader(packBytes, 0))
            return packBytes;

        if (packBytes.Length >= 6
            && packBytes.AsSpan(0, 4).SequenceEqual(EditorMagic)
            && IsZipHeader(packBytes, 6))
        {
            if (packBytes[4] > ContainerVersion)
                throw new InvalidDataException($"发布包容器版本过高（{packBytes[4]}），请升级播放器。");
            return packBytes.AsSpan(6).ToArray();
        }

        if (packBytes.Length >= AesMagicBytes.Length + 16 + 32
            && packBytes.AsSpan(0, AesMagicBytes.Length).SequenceEqual(AesMagicBytes))
            return DecryptAes(packBytes);

        throw new InvalidDataException("无法识别的发布包格式。");
    }

    public static bool IsZipHeader(byte[] data, int offset)
    {
        if (data.Length < offset + 4) return false;
        return data[offset] == 0x50 && data[offset + 1] == 0x4B
               && (data[offset + 2] is 0x03 or 0x05 or 0x07);
    }

    private static byte[] DecryptAes(byte[] packBytes)
    {
        var iv = packBytes.AsSpan(8, 16).ToArray();
        var hmac = packBytes.AsSpan(24, 32).ToArray();
        var cipher = packBytes.AsSpan(56).ToArray();

        var packKey = Derive("XHZN.pack.v1.aes:");
        var hmacKey = Derive("XHZN.pack.v1.mac:");

        var macSrc = new byte[iv.Length + cipher.Length];
        Buffer.BlockCopy(iv, 0, macSrc, 0, iv.Length);
        Buffer.BlockCopy(cipher, 0, macSrc, iv.Length, cipher.Length);
        var expected = HMACSHA256.HashData(hmacKey, macSrc);
        if (!CryptographicOperations.FixedTimeEquals(expected, hmac))
            throw new InvalidDataException("发布包校验失败，文件可能已损坏或被改动。");

        using var aes = Aes.Create();
        aes.Key = packKey;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static byte[] Derive(string prefix) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(prefix + AppSecret));
}
