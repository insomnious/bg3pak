// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;
using Newtonsoft.Json;

string filePath = @"C:\Work\bg3pak\paks\ImpUI_26922ba9-6018-5252-075d-7ff2ba6ed879.pak";

using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
using (BinaryReader br = new BinaryReader(fs))
{
    var magic = br.ReadBytes(4);

    var data = new Header
    {
        Version = br.ReadUInt32(),
        FileListOffset = br.ReadUInt64(),
        FileListSize = br.ReadUInt32(),
        Flags = br.ReadByte(),
        Priority = br.ReadByte(),
        Md5 = br.ReadBytes(16), // Assuming MD5 is 16 bytes
        numParts = br.ReadUInt16()
    };

    ReadCompressedFileList(br, (int)data.FileListOffset);

    // Process the data as needed
    Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
}


void ReadCompressedFileList(BinaryReader br, int offset)
{
    br.BaseStream.Seek(offset, SeekOrigin.Begin);

    var numOfFiles = br.ReadInt32();
    var compressedSize = br.ReadInt32();

    Console.WriteLine(numOfFiles);
    Console.WriteLine(compressedSize);

    //int fileBufferSize = Marshal.SizeOf(typeof(FileEntry18)) * numOfFiles;
    int fileBufferSize = compressedSize * 50;

    var compressed = br.ReadBytes((int)compressedSize);

    // Start with a buffer that is large enough
    byte[] decompressed = new byte[fileBufferSize];

    int decodedBytes = LZ4Codec.Decode(compressed, 0, compressed.Length, decompressed, 0, decompressed.Length);

    Console.WriteLine($"DecodedBytes {decodedBytes}");
    
    if (decodedBytes == 0)
    {
        throw new InvalidOperationException("Decompression failed.");
    }

    // Resize the buffer to the actual decompressed size
    Array.Resize(ref decompressed, decodedBytes);

    // Process the uncompressed data as needed
    Console.WriteLine("Decompression successful.");

    string tempFilePath = @"C:\Work\bg3pak\paks\temp.bin";
    File.WriteAllBytes(tempFilePath, decompressed);
    
    using var ms = new MemoryStream(decompressed);
    using var msr = new BinaryReader(ms);

    var entries = new List<FileEntry18>();
    
    for(var i = 0; i < numOfFiles; i++)
    {
        var entry = new FileEntry18
        {
            Name = Encoding.UTF8.GetString(msr.ReadBytes(256)).TrimEnd('\0'),
            OffsetInFile1 = msr.ReadUInt32(),
            OffsetInFile2 = msr.ReadUInt16(),
            ArchivePart = msr.ReadByte(),
            Flags = msr.ReadByte(),
            SizeOnDisk = msr.ReadUInt32(),
            UncompressedSize = msr.ReadUInt32()
        };
        
        Console.WriteLine(JsonConvert.SerializeObject(entry, Formatting.Indented));
        
        entries.Add(entry);
    }
}

public struct Header
{
    public uint Version;
    public ulong FileListOffset;
    public uint FileListSize;
    public byte Flags;
    public byte Priority;
    public byte[] Md5;
    public ushort numParts;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FileEntry18
{
    //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public string Name;
    
    public uint OffsetInFile1;
    public ushort OffsetInFile2;
    public byte ArchivePart;
    public byte Flags;
    public uint SizeOnDisk;
    public uint UncompressedSize;
}