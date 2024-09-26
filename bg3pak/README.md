# bg3pak

A WIP tool to read the contents of `.pak` files used in modding Baldur's Gate 3. This was primarily developed as part of Baldur's Gate 3 support for the [Nexus Mods app](https://github.com/Nexus-Mods/NexusMods.App), the new in-development mod manager created by Nexus Mods.

Massive thanks to [Norbyte](https://github.com/Norbyte) for their work on [LSLib](https://github.com/Norbyte/lslib) and the [BG3SE](https://github.com/Norbyte/bg3se). You've been a huge help getting this tool off the ground and Baldur's Gate 3 modding would be nothing with you.

## File Format

The `.pak` file is a file format used by Baldur's Gate 3 for distribution of mods. The file format is primarily a LZ4-compressed list of files. The data of the actual files are also compressed using LZ4. Here is a work-in-progress breakdown of the binary file format.

Version 18 is the latest version and this tool supports v15 and greater.

### Header

The header contains the following fields:

If version = 15, header is 38 bytes

| Name           | Type     | Description                             |
|----------------|----------|-----------------------------------------|
| magic          | `char[4]` | Magic bytes `LSPK`                      |
| version        | `uint`   | Version number                          |
| fileListOffset | `uint64` | Position where the list of files begins |
| fileListSize   | `uint`   | Size of fileList data section           |  
| flags          | `byte`   | Flags                                   |
| priority       | `byte`   | Priority                                |
| md5            | `char[16]` | MD5 hash of ...                         |

If version >= 16, header is 40 bytes and has an additional field:

| Name           | Type     | Description |
|----------------|----------|----------|
| numParts       | `ushort` |          |

## File List

The file list is a list of files that are contained within the `.pak` file. The file list is an array of `FileEntry` structs, starts at `fileListOffset`, is `fileListSize` bytes long and is compressed using LZ4.

### FileEntry 

#### Version 15 - 296 bytes

| Name             | Type     | Description         |
|------------------|----------|---------------------|
| Name             | `string` | Fixed size of `256` |
| OffsetInFile     | `ulong`  |                     |
| SizeOnDisk       | `ulong`  |                     |
| UncompressedSize | `ulong`  |                     |
| ArchivePart      | `uint`   |                     |
| Flags            | `uint`   |                     |
| Crc              | `uint`   |                     |
| Unknown2         | `uint`   |                     |

#### Version 18 - 272 bytes

| Name             | Type     | Description         |
|------------------|----------|---------------------|
| Name             | `string` | Fixed size of `256` |
| OffsetInFile1    | `uint`   |                     |
| OffsetInFile2    | `ushort` |                     |
| ArchivePart      | `byte`   |                     |
| Flags            | `byte`   |                     |
| SizeOnDisk       | `uint`   |                     |
| UncompressedSize | `uint`   |                     |

## File Data

Each file that is stored within the `pak` file can be extracted using the above FileEntry information. The data for each file starts at it's `OffsetInFile` and is `SizeOnDisk` bytes long.