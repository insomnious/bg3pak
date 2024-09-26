// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using K4os.Compression.LZ4;
using Newtonsoft.Json;


string filePath = @"C:\Users\insom\Downloads\5e Spells-125-2-1-0-6-1724104388\5eSpells.pak";

PakFileLoader pakLoader = new PakFileLoader();
pakLoader.LoadFromFile(filePath);


public class PakFileLoader
{
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
    
    private const string MAGIC_BYTES = "LSPK";
    private const int FILE_ENTRY_SIZE = 272;

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

        var data = new Header
        {
            Version = br.ReadUInt32(),
            FileListOffset = br.ReadUInt64(),
            FileListSize = br.ReadUInt32(),
            Flags = br.ReadByte(),
            Priority = br.ReadByte(),
            Md5 = br.ReadBytes(16),
            numParts = br.ReadUInt16()
        };

        // display header
        Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
        
        ReadCompressedFileList(br, (int)data.FileListOffset);
    }

    private void ReadCompressedFileList(BinaryReader br, int offset)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);

        var numOfFiles = br.ReadInt32();
        var compressedSize = br.ReadInt32();

        Console.WriteLine($"Number of files: {numOfFiles}");
        Console.WriteLine($"Compressed size: {compressedSize}");

        var decompressedSize = numOfFiles * FILE_ENTRY_SIZE;
        var compressed = br.ReadBytes(compressedSize);

        byte[] decompressed = new byte[decompressedSize];
        int decodedBytes = LZ4Codec.Decode(compressed, 0, compressed.Length, decompressed, 0, decompressed.Length);

        Console.WriteLine($"DecodedBytes {decodedBytes}");

        if (decodedBytes == 0)
        {
            throw new InvalidOperationException("Decompression failed.");
        }

        //Array.Resize(ref decompressed, decodedBytes);
        Console.WriteLine("Decompression successful.");

        // write temp bytes so we can see what we're working with
        File.WriteAllBytes(@"C:\Work\bg3pak\paks\temp.bin", decompressed);

        // new mem stream from decompress bytes
        using var ms = new MemoryStream(decompressed);
        using var msr = new BinaryReader(ms);

        // built up list of file entries
        var entries = new List<FileEntry18>();

        for (var i = 0; i < numOfFiles; i++)
        {
            FileEntry18 entry = new FileEntry18
            {
                Name = Encoding.UTF8.GetString(msr.ReadBytes(256)).TrimEnd('\0'),
                OffsetInFile1 = msr.ReadUInt32(),
                OffsetInFile2 = msr.ReadUInt16(),
                ArchivePart = msr.ReadByte(),
                Flags = msr.ReadByte(),
                SizeOnDisk = msr.ReadUInt32(),
                UncompressedSize = msr.ReadUInt32()
            };

            entries.Add(entry);
        }
        
        // look through file entries for meta.lsx
        var metaLsx = entries.FirstOrDefault(e => e.Name.Contains("meta.lsx"));

        // if we have something, then read the data
        if (metaLsx.Name != null)
        {
            Console.WriteLine(JsonConvert.SerializeObject(metaLsx, Formatting.Indented));

            byte[] metaLsxData = ReadFileEntryData(br, metaLsx, (int)metaLsx.OffsetInFile1, (int)metaLsx.SizeOnDisk);

            string metaLsxFilePath = @"C:\Work\bg3pak\paks\meta.lsx";
            File.WriteAllBytes(metaLsxFilePath, metaLsxData);
        }
        else
        {
            Console.WriteLine("meta.lsx not found.");
        }
    }

    private byte[] ReadFileEntryData(BinaryReader br, FileEntry18 fileMeta, int offset, int size)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);

        var data = br.ReadBytes(size);
        byte[] decompressed = new byte[fileMeta.UncompressedSize];

        int decodedBytes = LZ4Codec.Decode(data, 0, data.Length, decompressed, 0, decompressed.Length);

        return decompressed;
    }
}