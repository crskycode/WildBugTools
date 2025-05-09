using System;
using System.IO;
using System.Runtime.CompilerServices;

#pragma warning disable IDE0290

namespace CommonLib
{
    public class WpxDecompressor
    {
        private readonly Stream m_input;
        private readonly int m_format;
        private readonly int m_base_length;
        private readonly int m_output_length;
        private int m_bits;
        private int m_bit_remaining;
        private byte[] m_output;

        public WpxDecompressor(Stream input, int format, int base_length, int output_length)
        {
            m_input = input;
            m_output = [];
            m_format = format;
            m_base_length = base_length;
            m_output_length = output_length;
            m_bits = 0;
            m_bit_remaining = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadByte()
        {
            var b = m_input.ReadByte();

            if (b == -1)
            {
                throw new EndOfStreamException();
            }

            return (byte)b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadBit()
        {
            if (m_bit_remaining == 0)
            {
                m_bits = ReadByte();
                m_bit_remaining = 8;
            }

            var bit = (m_bits & 0x80) >> 7;

            m_bits <<= 1;
            m_bit_remaining--;

            return bit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadLength()
        {
            var bit_count = 1;
            var length = 1;

            while (ReadBit() == 0)
            {
                bit_count++;
            }

            for (var i = 0; i < bit_count; i++)
            {
                length += ReadBit() + length;
            }

            length--;

            return length;
        }

        private static byte[] CreateTransformTable()
        {
            var table = new byte[0x10000];

            for (var i = 0; i < 256; i++)
            {
                var q = (byte)(-1 - i);

                for (var j = 0; j < 256; j++)
                {
                    table[256 * i + j] = q--;
                }
            }

            return table;
        }

        private byte[,] ReadShiftTable()
        {
            var table = new byte[32768, 2];
            var input = new byte[128];

            m_input.Read(input, 0, input.Length);

            m_bits = ReadByte();
            m_bit_remaining = 8;

            for (var i = 0; i < 128; i++)
            {
                var bits = input[i];

                for (var j = 0; j < 2; j++)
                {
                    var count = bits & 15;

                    if (count != 0)
                    {
                        var index = 0;

                        for (var q = 0; q < count; q++)
                        {
                            index += ReadBit() + index;
                        }

                        if (count != 15)
                        {
                            index <<= 15 - count;
                        }

                        table[index, 0] = (byte)(count);
                        table[index, 1] = (byte)(2 * i + j);
                    }

                    bits >>= 4;
                }
            }

            return table;
        }

        private byte[] Decompress1()
        {
            // [MultiByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            m_bits = ReadByte();
            m_bit_remaining = 8;

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    m_output[dst] = ReadByte();
                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                int offset;
                int length;

                if (ReadBit() != 0)
                {
                    offset = ReadByte();
                    length = 2;
                }
                else
                {
                    var lo = ReadByte();
                    var hi = ReadByte();
                    offset = lo | (hi << 8);
                    length = 3;
                }

                offset = dst - 1 - offset;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        private byte[] Decompress2()
        {
            // [ShiftTable]
            // [MultiByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            var shift_table = ReadShiftTable();

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    var index = 0;

                    for (int bit = 0x4000, pos = 1; bit != 0; bit >>= 1, pos++)
                    {
                        if (ReadBit() != 0)
                        {
                            index |= bit;
                        }

                        if (pos == shift_table[index, 0])
                        {
                            break;
                        }
                    }

                    m_output[dst] = shift_table[index, 1];

                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                int offset;
                int length;

                if (ReadBit() != 0)
                {
                    offset = ReadByte();
                    length = 2;
                }
                else
                {
                    var lo = ReadByte();
                    var hi = ReadByte();
                    offset = lo | (hi << 8);
                    length = 3;
                }

                offset = dst - 1 - offset;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        private byte[] Decompress4()
        {
            // [ShiftTable]
            // [Transform]
            // [MultiByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            var transform_table = CreateTransformTable();
            var shift_table = ReadShiftTable();

            var val_pos = 0x4000;

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    val_pos &= ~0xFF00;
                    val_pos |= m_output[dst - m_base_length] << 8;

                    var index = 0;

                    for (int bit = 0x4000, pos = 1; bit != 0; bit >>= 1, pos++)
                    {
                        if (ReadBit() != 0)
                        {
                            index |= bit;
                        }

                        if (pos == shift_table[index, 0])
                        {
                            break;
                        }
                    }

                    var shift = shift_table[index, 1];
                    var val = transform_table[val_pos + shift];

                    if (shift != 0)
                    {
                        Buffer.BlockCopy(transform_table, val_pos, transform_table, val_pos + 1, shift);
                        transform_table[val_pos] = val;
                    }

                    m_output[dst] = val;

                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                int offset;
                int length;

                if (ReadBit() != 0)
                {
                    offset = ReadByte();
                    length = 2;
                }
                else
                {
                    var lo = ReadByte();
                    var hi = ReadByte();
                    offset = lo | (hi << 8);
                    length = 3;
                }

                offset = dst - 1 - offset;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        private byte[] Decompress8()
        {
            // [OneByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            m_bits = ReadByte();
            m_bit_remaining = 8;

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    m_output[dst] = ReadByte();
                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                var offset = dst - 1 - ReadByte();
                var length = 2;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        private byte[] Decompress10()
        {
            // [ShiftTable]
            // [OneByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            var shift_table = ReadShiftTable();

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    var index = 0;

                    for (int bit = 0x4000, pos = 1; bit != 0; bit >>= 1, pos++)
                    {
                        if (ReadBit() != 0)
                        {
                            index |= bit;
                        }

                        if (pos == shift_table[index, 0])
                        {
                            break;
                        }
                    }

                    m_output[dst] = shift_table[index, 1];

                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                var offset = dst - 1 - ReadByte();
                var length = 2;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        private byte[] Decompress12()
        {
            // [ShiftTable]
            // [Transform]
            // [OneByteOffset]

            m_output = new byte[m_output_length];

            var remaining = m_output_length;
            var dst = 0;

            for (var i = 0; i < m_base_length; i++)
            {
                m_output[dst + i] = ReadByte();
            }

            dst += m_base_length;
            remaining -= m_base_length;

            var aligned_base_length = (m_base_length + 3) & -4;

            m_input.Position += aligned_base_length - m_base_length;

            var transform_table = CreateTransformTable();
            var shift_table = ReadShiftTable();

            var val_pos = 0x4000;

            while (remaining > 0)
            {
                while (ReadBit() != 0)
                {
                    val_pos &= ~0xFF00;
                    val_pos |= m_output[dst - m_base_length] << 8;

                    var index = 0;

                    for (int bit = 0x4000, pos = 1; bit != 0; bit >>= 1, pos++)
                    {
                        if (ReadBit() != 0)
                        {
                            index |= bit;
                        }

                        if (pos == shift_table[index, 0])
                        {
                            break;
                        }
                    }

                    var shift = shift_table[index, 1];
                    var val = transform_table[val_pos + shift];

                    if (shift != 0)
                    {
                        Buffer.BlockCopy(transform_table, val_pos, transform_table, val_pos + 1, shift);
                        transform_table[val_pos] = val;
                    }

                    m_output[dst] = val;

                    dst++;
                    remaining--;

                    if (remaining == 0)
                    {
                        return m_output;
                    }
                }

                var offset = dst - 1 - ReadByte();
                var length = 2;

                if (ReadBit() == 0)
                {
                    length += ReadLength();
                }

                if (remaining < length)
                {
                    throw new EndOfStreamException();
                }

                for (var i = 0; i < length; i++)
                {
                    m_output[dst + i] = m_output[offset + i];
                }

                dst += length;
                remaining -= length;
            }

            return m_output;
        }

        public byte[] Decompress()
        {
            // 8 : OneByteOffset
            // 4 : Transform
            // 2 : ShiftTable
            // 1 : Compressed

            if ((m_format & 8) != 0)
            {
                if ((m_format & 4) != 0)
                {
                    return Decompress12();
                }
                else if ((m_format & 2) != 0)
                {
                    return Decompress10();
                }
                else
                {
                    return Decompress8();
                }
            }
            else if ((m_format & 4) != 0)
            {
                return Decompress4();
            }
            else if ((m_format & 2) != 0)
            {
                return Decompress2();
            }
            else
            {
                return Decompress1();
            }
        }
    }
}
