using System.IO.Compression;
using System.Text;

namespace Xhzn.PackFormat;

public static class PngWriter
{
    public static byte[] WriteRgb(int width, int height, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixel(x, y);
                raw.WriteByte(r);
                raw.WriteByte(g);
                raw.WriteByte(b);
            }
        }

        var compressed = ZlibCompress(raw.ToArray());
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(png, "IHDR", Hdr(width, height));
        WriteChunk(png, "IDAT", compressed);
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    public static byte[] Placeholder(int width = 640, int height = 360)
    {
        return WriteRgb(width, height, (x, y) =>
        {
            var border = x < 8 || y < 8 || x >= width - 8 || y >= height - 8;
            if (border) return ((byte)70, (byte)70, (byte)74);
            var stripe = ((x / 32) + (y / 32)) % 2 == 0;
            return stripe ? ((byte)48, (byte)48, (byte)52) : ((byte)40, (byte)40, (byte)44);
        });
    }

    private static byte[] Hdr(int w, int h)
    {
        var buf = new byte[13];
        WriteBe(buf, 0, w);
        WriteBe(buf, 4, h);
        buf[8] = 8;
        buf[9] = 2;
        return buf;
    }

    private static void WriteBe(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        var len = new byte[4];
        WriteBe(len, 0, data.Length);
        s.Write(len);
        s.Write(typeBytes);
        s.Write(data);
        var crcSrc = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcSrc, 0, 4);
        Buffer.BlockCopy(data, 0, crcSrc, 4, data.Length);
        var crc = Crc32(crcSrc);
        var crcBuf = new byte[4];
        WriteBe(crcBuf, 0, (int)crc);
        s.Write(crcBuf);
    }

    private static byte[] ZlibCompress(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data);
        var adler = Adler32(data);
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var c in data)
        {
            a = (a + c) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            var idx = (byte)((crc ^ b) & 0xFF);
            crc = CrcTable[idx] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
