using CommonLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ArcToolV2
{
    internal class Arc
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("ARCFORM2");

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

            // Read bucket table

            var buckets = new List<int>(256);

            for (var i = 0; i < 256; i++)
            {
                buckets.Add(reader.ReadInt32());
            }

            // Read entries

            var entries = new List<Entry>();

            for (var i = 0; i < 256; i++)
            {
                if (buckets[i] == 0)
                {
                    continue;
                }

                input.Position = buckets[i];

                while (true)
                {
                    var info = reader.ReadBytes(0x14);

                    if (info[8] != i)
                    {
                        break;
                    }

                    var name_bytes = reader.ReadBytes(info[9])
                        .TakeWhile(x => x != 0)
                        .ToArray();

                    entries.Add(new Entry
                    {
                        Name = encoding.GetString(name_bytes),
                        Position = BitConverter.ToInt32(info, 0),
                        Length = BitConverter.ToInt32(info, 4)
                    });
                }
            }

            // Extract entries

            Directory.CreateDirectory(outputPath);

            foreach (var entry in entries)
            {
                input.Position = entry.Position;

                var entryName = entry.Name;

                Console.WriteLine("Extract {0}", entryName);

                var data = reader.ReadBytes(entry.Length);

                var entryPath = Path.Combine(outputPath, entryName);
                var entryDirPath = Path.GetDirectoryName(entryPath) ?? string.Empty;

                Directory.CreateDirectory(entryDirPath);
                File.WriteAllBytes(entryPath, data);
            }

            // Done

            reader.Close();
        }

        class Entry
        {
            public string Name { get; set; } = string.Empty;
            public int Position { get; set; }
            public int Length { get; set; }
        }

        public static void Create(string filePath, string rootPath, Encoding encoding)
        {
            var source = Directory.GetFiles(rootPath, "*", SearchOption.TopDirectoryOnly);

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
                    Name = Path.GetFileName(path).ToUpperInvariant(),
                    Hash = ComputeHash(Path.GetFileName(path).ToUpperInvariant(), encoding)
                })
                .OrderBy(x => x.Hash)
                .ThenBy(x => x.Name)
                .ToList();

            if (entries.Any(x => GetNameLength(x.Name, encoding) > 255))
            {
                throw new Exception("Name too long.");
            }

            // Create file

            using var output = File.Create(filePath);
            using var writer = new BinaryWriter(output);

            // Write header

            writer.Write(Signature);
            writer.Write(0x1A204755425720);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);

            // Write bucket table

            var buckets = new int[256];

            for (var i = 0; i < buckets.Length; i++)
            {
                writer.Write(0);
            }

            // Write index

            var indexPos = Convert.ToInt32(output.Position);

            foreach (var entry in entries)
            {
                entry.Position = Convert.ToInt32(output.Position);

                if (buckets[entry.Hash] == 0)
                {
                    buckets[entry.Hash] = entry.Position;
                }

                writer.Write(0);
                writer.Write(0);
                writer.Write((byte)entry.Hash);
                writer.Write((byte)GetNameLength(entry.Name, encoding));
                writer.Write((short)0);
                writer.Write(0);
                writer.Write(0);
                writer.WriteNullTerminatedString(entry.Name, encoding);

                // We need write the tail padding bytes

                var alignedEndPos = (output.Position + 3) & ~3;

                while (output.Position < alignedEndPos)
                {
                    writer.Write((byte)0);
                }
            }

            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write((byte)0);

            var indexLength = Convert.ToInt32(output.Position - indexPos);

            // Add file data

            var dataPos = Convert.ToInt32(output.Position);

            foreach (var entry in entries)
            {
                Console.WriteLine("Add {0}", entry.Name);

                var data = File.ReadAllBytes(entry.LocalPath);

                entry.DataPosition = Convert.ToInt32(output.Position);
                entry.DataLength = data.Length;

                writer.Write(data);

                // We need write the tail padding bytes

                var alignedEndPos = (output.Position + 3) & ~3;

                while (output.Position < alignedEndPos)
                {
                    writer.Write((byte)0);
                }
            }

            // Update entry information

            foreach (var entry in entries)
            {
                output.Position = entry.Position;

                writer.Write(entry.DataPosition);
                writer.Write(entry.DataLength);
            }

            // Update bucket table

            output.Position = 0x24;

            for (var i = 0; i < buckets.Length; i++)
            {
                writer.Write(buckets[i]);
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

        private static int ComputeHash(string input, Encoding encoding)
        {
            var buffer = new byte[4];
            var bytes = encoding.GetBytes(input);

            Array.Copy(bytes, buffer, Math.Min(4, bytes.Length));

            var hash = buffer[0] + buffer[1] + buffer[2] + buffer[3];

            return (byte)hash;
        }

        private static int GetNameLength(string input, Encoding encoding)
        {
            var length = encoding.GetByteCount(input) + 1;
            length = (length + 3) & ~3;
            return length;
        }

        class PackEntry
        {
            public string LocalPath { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Hash { get; set; }
            public int Position { get; set; }
            public int DataPosition { get; set; }
            public int DataLength { get; set; }
        }
    }
}
