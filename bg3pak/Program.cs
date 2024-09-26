// See https://aka.ms/new-console-template for more information
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;
using Newtonsoft.Json;

//string pakFile = @"paks\ImpUI_26922ba9-6018-5252-075d-7ff2ba6ed879.pak";
string pakFile = @"assets/AllItems.pak";
string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pakFile);

PakFileLoader pakLoader = new PakFileLoader();
pakLoader.LoadFromFile(filePath);

public class PakFileLoader
{
    public struct Header16
    {
        public uint Version;
        public ulong FileListOffset;
        public uint FileListSize;
        public byte Flags;
        public byte Priority;
        public byte[] MD5;
        public ushort numParts;
    }
    public struct Header15
    {
        public uint Version;
        public ulong FileListOffset;
        public uint FileListSize;
        public byte Flags;
        public byte Priority;
        public byte[] MD5;
    }
    
    public struct CommonHeader
    {
        public uint Version;
        public ulong FileListOffset;
        public uint FileListSize;
        public byte Flags;
        public byte Priority;
        public byte[] MD5;
        public ushort numParts;
    }

    public struct FileEntry15
    {
        public string Name;
        public ulong OffsetInFile;
        public ulong SizeOnDisk;
        public ulong UncompressedSize;
        public uint ArchivePart;
        public uint Flags;
        public uint Crc;
        public uint Unknown2;
    }

    public struct FileEntry18
    {
        public string Name;
        public uint OffsetInFile1;
        public ushort OffsetInFile2;
        public byte ArchivePart;
        public byte Flags;
        public uint SizeOnDisk;
        public uint UncompressedSize;
    }

    public struct CommonFileEntry
    {
        public string Name;
        public uint OffsetInFile;
        public uint SizeOnDisk;
        public uint UncompressedSize;
    }
    
    private const string MAGIC_BYTES = "LSPK";
    private const int FILE_ENTRY_15_SIZE = 296;
    private const int FILE_ENTRY_18_SIZE = 272;

    public void LoadFromFile(string filePath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader br = new BinaryReader(fs))
        {
            Load(br);
        }
    }

    public void LoadFromByteArray(byte[] fileData)
    {
        using (MemoryStream ms = new MemoryStream(fileData))
        using (BinaryReader br = new BinaryReader(ms))
        {
            Load(br);
        }
    }

    private void Load(BinaryReader br)
    {
        var magic = br.ReadBytes(4);

        if (Encoding.UTF8.GetString(magic) != MAGIC_BYTES)
        {
            throw new Exception($"Not a valid BG3 PAK. Magic bytes ({MAGIC_BYTES}) not found.");
        }

        var header = new CommonHeader
        {
            Version = br.ReadUInt32(),
            FileListOffset = br.ReadUInt64(),
            FileListSize = br.ReadUInt32(),
            Flags = br.ReadByte(),
            Priority = br.ReadByte(),
            MD5 = br.ReadBytes(16)
        };
        
        if (header.Version == 16)
        {
            header.numParts = br.ReadUInt16();
        }
        
        Console.WriteLine($"MD5: {BitConverter.ToString(header.MD5).Replace("-", "").ToLower()}");
        // display header
        Console.WriteLine(JsonConvert.SerializeObject(header, Formatting.Indented));
        
        ReadCompressedFileList(br, (int)header.FileListOffset, header);
    }

    private void ReadCompressedFileList(BinaryReader br, int offset, CommonHeader header)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);

        var numOfFiles = br.ReadInt32();
        var compressedSize = br.ReadInt32();

        Console.WriteLine($"Number of files: {numOfFiles}");
        Console.WriteLine($"Compressed size: {compressedSize}");

        var decompressedSize = numOfFiles * FILE_ENTRY_15_SIZE;
        var compressed = br.ReadBytes(compressedSize);

        byte[] decompressed = new byte[decompressedSize];
        int decodedBytes = LZ4Codec.Decode(compressed, 0, compressed.Length, decompressed, 0, decompressed.Length);

        Console.WriteLine($"DecodedBytes {decodedBytes}");
        
        //Array.Resize(ref decompressed, decodedBytes);

        if (decodedBytes != decompressed.Length)
        {
            throw new InvalidOperationException("Decompression failed.");
        }

        //Array.Resize(ref decompressed, decodedBytes);
        Console.WriteLine("Decompression successful.");

        // write temp bytes so we can see what we're working with
        File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"assets\temp.bin") , decompressed);

        // new mem stream from decompress bytes
        using var ms = new MemoryStream(decompressed);
        using var msr = new BinaryReader(ms);
        
        // built up list of file entries
        var entries = new List<CommonFileEntry>();

        msr.BaseStream.Seek(0, SeekOrigin.Begin);
            
        for (var i = 0; i < numOfFiles; i++)
        {
            var entry = GetFileEntry(msr, (int) header.Version);

            //Console.WriteLine(JsonConvert.SerializeObject(entry, Formatting.Indented));
            
            entries.Add(entry);
        }
        
        // look through file entries for meta.lsx
        var metaLsx = entries.FirstOrDefault(e => e.Name.Contains("meta.lsx"));

        // if we have something, then read the data
        if (metaLsx.Name != null)
        {
            Console.WriteLine(JsonConvert.SerializeObject(metaLsx, Formatting.Indented));

            byte[] metaLsxData = ReadFileEntryData(br, metaLsx, (int)metaLsx.OffsetInFile, (int)metaLsx.SizeOnDisk);

            File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"assets\meta.lsx"), metaLsxData);
        }
        else
        {
            Console.WriteLine("meta.lsx not found.");
        }
    }
    
    CommonFileEntry GetFileEntry(BinaryReader br, int version)
    {
        switch (version)
        {
            case 15:
            {
                FileEntry15 entry = new FileEntry15
                {
                    Name = Encoding.UTF8.GetString(br.ReadBytes(256)).TrimEnd('\0'),
                    OffsetInFile = br.ReadUInt64(),
                    SizeOnDisk = br.ReadUInt64(),
                    UncompressedSize = br.ReadUInt64(),
                    ArchivePart = br.ReadUInt32(),
                    Flags = br.ReadUInt32(),
                    Crc = br.ReadUInt32(),
                    Unknown2 = br.ReadUInt32()
                }; 
                
                return new CommonFileEntry
                {
                    Name = entry.Name,
                    OffsetInFile = (uint) entry.OffsetInFile,
                    SizeOnDisk = (uint) entry.SizeOnDisk,
                    UncompressedSize = (uint) entry.UncompressedSize
                };
            }
            case 18:
            {
                var entry = new FileEntry18
                {
                    Name = Encoding.UTF8.GetString(br.ReadBytes(256)).TrimEnd('\0'),
                    OffsetInFile1 = br.ReadUInt32(),
                    OffsetInFile2 = br.ReadUInt16(),
                    ArchivePart = br.ReadByte(),
                    Flags = br.ReadByte(),
                    SizeOnDisk = br.ReadUInt32(),
                    UncompressedSize = br.ReadUInt32()
                };
                
                return new CommonFileEntry
                {
                    Name = entry.Name,
                    OffsetInFile = entry.OffsetInFile1,
                    SizeOnDisk = entry.SizeOnDisk,
                    UncompressedSize = entry.UncompressedSize
                };
            }
            default:
                throw new Exception("Invalid version.");
        }
    }

    private byte[] ReadFileEntryData(BinaryReader br, CommonFileEntry fileMeta, int offset, int size)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);

        var data = br.ReadBytes(size);
        byte[] decompressed = new byte[fileMeta.UncompressedSize];

        int decodedBytes = LZ4Codec.Decode(data, 0, data.Length, decompressed, 0, decompressed.Length);

        return decompressed;
    }
}