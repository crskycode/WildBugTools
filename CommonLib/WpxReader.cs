using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

#pragma warning disable IDE0017
#pragma warning disable IDE0270

namespace CommonLib
{
    public class WpxReader : IDisposable
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("WPX");

        private readonly string m_type;
        private readonly Stream m_input;
        private readonly List<Entry> m_entries;

        public WpxReader(string filePath, string type)
        {
            m_type = type;
            m_input = File.OpenRead(filePath);
            m_entries = [];
            Parse();
        }

        private void Parse()
        {
            var reader = new BinaryReader(m_input, Encoding.UTF8, true);

            var signature = reader.ReadBytes(3);

            if (!signature.SequenceEqual(Signature))
            {
                throw new Exception("Not a WPX file.");
            }

            // Skip unnecessary bytes

            while (true)
            {
                var mark = reader.ReadByte();

                if (mark == 0x1A)
                {
                    break;
                }
            }

            // Create type identifier

            var identifier = new byte[4];
            var buf = Encoding.ASCII.GetBytes(m_type);
            Array.Copy(buf, identifier, buf.Length);

            // Read information

            var type = reader.ReadBytes(4);
            var unk2 = reader.ReadInt32();
            var unk3 = reader.ReadByte();
            var unk4 = reader.ReadByte();
            var entry_count = reader.ReadByte();
            var entry_length = reader.ReadByte();

            if (!type.SequenceEqual(identifier))
            {
                throw new Exception("Invalid WPX file.");
            }

            // Read entries

            var offset = m_input.Position;

            for (var i = 0; i < entry_count; i++)
            {
                m_input.Position = offset;

                // Read entry information

                var entry = new Entry();

                entry.Id = reader.ReadByte();
                entry.Format = reader.ReadByte();

                m_input.Position += 2; // gap

                entry.Position = reader.ReadInt32();
                entry.UncompressedLength = reader.ReadInt32();
                entry.CompressedLength = reader.ReadInt32();

                offset += entry_length;

                // Check entry information

                if (entry.Position <= 0 ||
                    entry.Position >= m_input.Length ||
                    entry.UncompressedLength <= 0 ||
                    entry.CompressedLength < 0)
                {
                    throw new Exception("Invalid WPX file.");
                }

                m_entries.Add(entry);
            }

            // Done

            reader.Close();
        }

        public bool Contains(int entryId)
        {
            return m_entries.Any(entry => entry.Id == entryId);
        }

        public byte[] Read(int entryId)
        {
            var entry = m_entries.FirstOrDefault(entry => entry.Id == entryId);

            if (entry == null)
            {
                throw new ArgumentOutOfRangeException(nameof(entryId));
            }

            if (entry.Format != 0 && entry.CompressedLength != 0)
            {
                m_input.Position = entry.Position;

                if (m_input.Position + entry.CompressedLength > m_input.Length)
                {
                    throw new EndOfStreamException();
                }

                var decompressor = new WpxDecompressor(m_input, entry.Format, 1, 0, entry.UncompressedLength);
                var data = decompressor.Decompress();

                return data;
            }
            else
            {
                var data = new byte[entry.UncompressedLength];

                if (m_input.Read(data, 0, data.Length) != data.Length)
                {
                    throw new EndOfStreamException();
                }

                return data;
            }
        }

        public byte[] ReadImage(int entryId, int base_length, int stride)
        {
            var entry = m_entries.FirstOrDefault(entry => entry.Id == entryId);

            if (entry == null)
            {
                throw new ArgumentOutOfRangeException(nameof(entryId));
            }

            if (entry.Format != 0 && entry.CompressedLength != 0)
            {
                m_input.Position = entry.Position;

                if (m_input.Position + entry.CompressedLength > m_input.Length)
                {
                    throw new EndOfStreamException();
                }

                var decompressor = new WpxDecompressor(m_input, entry.Format, base_length, stride, entry.UncompressedLength);
                var data = decompressor.DecompressImage();

                return data;
            }
            else
            {
                var data = new byte[entry.UncompressedLength];

                if (m_input.Read(data, 0, data.Length) != data.Length)
                {
                    throw new EndOfStreamException();
                }

                return data;
            }
        }

        private class Entry
        {
            public int Id { get; set; }
            public int Format { get; set; }
            public int Position { get; set; }
            public int CompressedLength { get; set; }
            public int UncompressedLength { get; set; }
        }

        #region IDisposable

        private bool m_disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    m_input.Close();
                    m_input.Dispose();
                }

                m_disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
