using CommonLib;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ArcTool
{
    internal class Arc
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("ARCFORM4");

        public static void Extract(string filePath, string outputPath, Encoding encoding)
        {
            using var input = File.OpenRead(filePath);
            using var reader = new BinaryReader(input);

            var signature = reader.ReadBytes(8);

            if (!signature.SequenceEqual(Signature))
            {
                throw new Exception("Not a valid WBP file.");
            }

            // Read header

            input.Position = 0x10;

            var entryCount = reader.ReadInt32();
            var indexPos = reader.ReadInt32();
            var indexLength = reader.ReadInt32();
            var dataPos = reader.ReadInt32();
            reader.ReadInt32(); // unk

            // Read directory position table

            for (var i = 0; i < 256; i++)
            {
                reader.ReadInt32();
            }

            // Read entry position table

            for (var i = 0; i < 256; i++)
            {
                reader.ReadInt32();
            }

            // Read directories

            var dirEntries = new Dictionary<int, DirEntry>();

            var indexEndPos = indexPos + indexLength;

            input.Position = indexPos;

            while (input.Position < indexEndPos)
            {
                var hash = reader.ReadByte();
                var nameLength = reader.ReadByte();

                if (nameLength == 0)
                {
                    input.Position += 2;
                    break;
                }

                var id = reader.ReadUInt16();
                var name = reader.ReadNullTerminatedString(encoding);

                input.AlignPosition(4);

                dirEntries.Add(id, new DirEntry
                {
                    Hash = hash,
                    Path = name,
                });
            }

            // Read entries

            var entries = new List<Entry>(entryCount);

            while (input.Position < indexEndPos)
            {
                var hash = reader.ReadByte();
                var nameLength = reader.ReadByte();

                if (nameLength == 0)
                {
                    input.Position += 18;
                    break;
                }

                var dirId = reader.ReadUInt16();
                var position = reader.ReadInt32();
                var length = reader.ReadInt32();
                var time = reader.ReadInt64();
                var name = reader.ReadNullTerminatedString(encoding);

                input.AlignPosition(4);

                entries.Add(new Entry
                {
                    Hash = hash,
                    DirId = dirId,
                    Name = name,
                    Position = position,
                    Length = length,
                    Time = DateTime.FromFileTime(time),
                });
            }

            // Extract entries

            Directory.CreateDirectory(outputPath);

            foreach (var entry in entries)
            {
                input.Position = entry.Position;

                var dirPath = dirEntries[entry.DirId].Path;

                if (dirPath.StartsWith('\\'))
                {
                    dirPath = dirPath[1..];
                }

                if (dirPath.EndsWith('\\'))
                {
                    dirPath = dirPath[..^1];
                }

                var entryName = Path.Combine(dirPath, entry.Name);

                Console.WriteLine("Extract {0}", entryName);

                var data = reader.ReadBytes(entry.Length);

                var entryPath = Path.Combine(outputPath, entryName);
                var entryDirPath = Path.GetDirectoryName(entryPath) ?? string.Empty;

                Directory.CreateDirectory(entryDirPath);
                File.WriteAllBytes(entryPath, data);
                File.SetLastWriteTime(entryPath, entry.Time);
            }

            // Done

            reader.Close();
        }

        class DirEntry
        {
            public int Hash { get; set; }
            public string Path { get; set; } = string.Empty;
        }

        class Entry
        {
            public int Hash { get; set; }
            public int DirId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Position { get; set; }
            public int Length { get; set; }
            public DateTime Time { get; set; }
        }

        public static void Create(string filePath, string rootPath, Encoding encoding)
        {
            var source = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);

            if (source.Length == 0)
            {
                throw new Exception("No files were found to package.");
            }

            // Create entries

            var entries = source
                .Select(x => x.ToUpperInvariant())
                .Select(path => new PackEntry
                {
                    LocalPath = path,
                    Path = MakeEntryPath(Path.GetRelativePath(rootPath, path)),
                    Name = Path.GetFileName(path),
                    Hash = ComputeHash(Path.GetFileName(path), encoding)
                })
                .OrderBy(x => x.Hash)
                .ThenBy(x => x.Name)
                .ToList();

            if (entries.Any(x => encoding.GetByteCount(x.Path) > 255 ||
                encoding.GetByteCount(x.Name) > 255))
            {
                throw new Exception("Path too long.");
            }

            // Extract unique path

            var dirPaths = entries.Select(x => x.Path)
                .Distinct()
                .Order()
                .ToList();

            if (dirPaths.Count > ushort.MaxValue)
            {
                throw new Exception("Too many paths.");
            }

            // Create directory entries

            var dirEntries = new List<PackDirEntry>(dirPaths.Count);

            for (var i = 0; i < dirPaths.Count; i++)
            {
                dirEntries.Add(new PackDirEntry
                {
                    Hash = ComputeHash(dirPaths[i], encoding),
                    Id = i,
                    Path = dirPaths[i],
                });
            }

            dirEntries = dirEntries.OrderBy(x => x.Hash).ThenBy(x => x.Path)
                .ToList();

            // Get directory ID for each entries

            var dirMap = dirEntries.ToFrozenDictionary(x => x.Path, x => x);

            foreach (var item in entries)
            {
                item.DirId = dirMap[item.Path].Id;
            }

            // Create file

            using var output = File.Create(filePath);
            using var writer = new BinaryWriter(output);

            // Write header

            writer.Write(Signature);
            writer.Write(0x1A204755425720u);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            // Write position table

            var dirPosTable = new int[256];

            for (var i = 0; i < dirPosTable.Length; i++)
            {
                writer.Write(0);
            }

            var entryPosTable = new int[256];

            for (var i = 0; i < entryPosTable.Length; i++)
            {
                writer.Write(0);
            }

            // Write directory information

            var indexPos = Convert.ToInt32(output.Position);

            foreach (var item in dirEntries)
            {
                item.Position = Convert.ToInt32(output.Position);

                if (dirPosTable[item.Hash] == 0)
                {
                    dirPosTable[item.Hash] = item.Position;
                }

                writer.Write((byte)item.Hash);
                writer.Write((byte)encoding.GetByteCount(item.Path));
                writer.Write((ushort)item.Id);
                writer.WriteNullTerminatedString(item.Path, encoding);

                output.AlignPosition(4);
            }

            writer.Write(0);

            // Write entry information

            foreach (var item in entries)
            {
                item.Position = Convert.ToInt32(output.Position);

                if (entryPosTable[item.Hash] == 0)
                {
                    entryPosTable[item.Hash] = item.Position;
                }

                writer.Write((byte)item.Hash);
                writer.Write((byte)encoding.GetByteCount(item.Name));
                writer.Write((ushort)item.DirId);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.WriteNullTerminatedString(item.Name, encoding);

                output.AlignPosition(4);
            }

            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            var indexLength = Convert.ToInt32(output.Position - indexPos);

            // Add file data

            var dataPos = Convert.ToInt32(output.Position);

            foreach (var item in entries)
            {
                Console.WriteLine("Add {0}", item.LocalPath);

                var data = File.ReadAllBytes(item.LocalPath);

                item.DataPosition = Convert.ToInt32(output.Position);
                item.DataLength = data.Length;
                item.Time = File.GetLastWriteTime(item.LocalPath);

                writer.Write(data);

                // We need write the tail padding bytes

                var alignedEndPos = (output.Position + 3) & ~3;

                while (output.Position < alignedEndPos)
                {
                    writer.Write((byte)0);
                }
            }

            // Update entry information

            foreach (var item in entries)
            {
                output.Position = item.Position + 4;

                writer.Write(item.DataPosition);
                writer.Write(item.DataLength);
                writer.Write(item.Time.ToFileTime());
            }

            // Update position table

            output.Position = 0x24;

            for (var i = 0; i < dirPosTable.Length; i++)
            {
                writer.Write(dirPosTable[i]);
            }

            for (var i = 0; i < entryPosTable.Length; i++)
            {
                writer.Write(entryPosTable[i]);
            }

            // Update header

            output.Position = 0x10;

            writer.Write(entries.Count);
            writer.Write(indexPos);
            writer.Write(indexLength);
            writer.Write(dataPos);

            // Done

            writer.Flush();
            writer.Close();
        }

        private static string MakeEntryPath(string input)
        {
            var path = Path.GetDirectoryName(input);

            if (string.IsNullOrEmpty(path))
                path = "\\";
            else
                path = "\\" + path + "\\";

            return path;
        }

        private static int ComputeHash(string input, Encoding encoding)
        {
            var data = encoding.GetBytes(input);
            byte hash = 0;

            for (var i = 0; i < data.Length; i++)
            {
                hash += data[i];
            }

            return hash;
        }

        class PackEntry
        {
            public string LocalPath { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Hash { get; set; }
            public int DirId { get; set; }
            public int Position { get; set; }
            public int DataPosition { get; set; }
            public int DataLength { get; set; }
            public DateTime Time { get; set; }
        }

        class PackDirEntry
        {
            public int Hash { get; set; }
            public int Id { get; set; }
            public string Path { get; set; } = string.Empty;
            public int Position { get; set; }
        }
    }
}
