using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable IDE0290

namespace CommonLib
{
    public class WpxWriter
    {
        private readonly string m_type;
        private readonly List<Entry> m_entries = [];

        public WpxWriter(string type)
        {
            m_type = type;
        }

        public void AddEntry(int id, string filePath)
        {
            if (m_entries.Count >= 255)
            {
                throw new InvalidOperationException("Too many entries.");
            }

            m_entries.Add(new Entry
            {
                Id = id,
                SourcePath = filePath,
            });
        }

        public void AddEntry(int id, byte[] data)
        {
            if (m_entries.Count >= 255)
            {
                throw new InvalidOperationException("Too many entries.");
            }

            m_entries.Add(new Entry
            {
                Id = id,
                UncompressedLength = data.Length,
                SourceData = data,
            });
        }

        public void Save(string filePath)
        {
            using var output = File.Create(filePath);
            using var writer = new BinaryWriter(output);

            // Create type identifier
            var type = new byte[4];
            var buf = Encoding.ASCII.GetBytes(m_type);
            Array.Copy(buf, type, buf.Length);

            // Write header

            writer.Write(0x1A585057);   // WPX
            writer.Write(type);
            writer.Write(0x10);         // Unk
            writer.Write((byte)1);      // Unk
            writer.Write((byte)0);      // Unk
            writer.Write(Convert.ToByte(m_entries.Count));
            writer.Write((byte)0x10);   // Entry size

            // Fill index space

            var indexPos = output.Position;

            for (var i = 0; i < m_entries.Count; i++)
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }

            // Write entry data

            foreach (var entry in m_entries)
            {
                entry.Position = Convert.ToInt32(output.Position);

                if (entry.SourceData.Length != 0)
                {
                    writer.Write(entry.SourceData);
                }
                else if (!string.IsNullOrEmpty(entry.SourcePath))
                {
                    var data = File.ReadAllBytes(entry.SourcePath);
                    entry.UncompressedLength = data.Length;
                    writer.Write(data);
                }
            }

            // Write index

            output.Position = indexPos;

            foreach (var entry in m_entries)
            {
                writer.Write((byte)entry.Id);
                writer.Write((byte)entry.Format);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write(entry.Position);
                writer.Write(entry.UncompressedLength);
                writer.Write(entry.CompressedLength);
            }

            // Done

            writer.Flush();
            writer.Close();
        }

        class Entry
        {
            public int Id { get; set; }
            public int Format { get; set; }
            public int Position { get; set; }
            public int CompressedLength { get; set; }
            public int UncompressedLength { get; set; }

            // Helpers
            public string SourcePath { get; set; } = string.Empty;
            public byte[] SourceData { get; set; } = [];
        }
    }
}
