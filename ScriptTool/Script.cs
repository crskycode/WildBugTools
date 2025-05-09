using CommonLib;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0017

namespace ScriptTool
{
    internal partial class Script
    {
        private static readonly byte[] Signature = Encoding.ASCII.GetBytes("WBUG_SCN v2.0");

        private readonly Header m_header = new();
        private readonly List<Label> m_labels = [];
        private readonly List<string> m_source_files = [];
        private readonly List<Command> m_commands = [];
        private readonly List<ConstantString> m_constant_strings = [];

        public void Load(string filePath, Encoding encoding)
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            var signature = reader.ReadBytes(Signature.Length);

            if (!signature.SequenceEqual(Signature))
            {
                // Try unpack script from WPX
                reader.Close();

                using var wpx = new WpxReader(filePath);

                const int scnId = 2;

                if (!wpx.Contains(scnId))
                {
                    throw new Exception("This WPX does not contain script.");
                }

                var data = wpx.Read(scnId);
                wpx.Dispose();

                var ms = new MemoryStream(data);
                Parse(ms, encoding);

                return;
            }
            else
            {
                stream.Position = 0;
                Parse(stream, encoding);
            }

            reader.Close();
        }

        private void Parse(Stream input, Encoding encoding)
        {
            using var reader = new BinaryReader(input);

            var signature = reader.ReadBytes(Signature.Length);

            if (!signature.SequenceEqual(Signature))
            {
                throw new Exception("Not a valid script file.");
            }

            input.Position = 0x10;

            m_header.Flags = reader.ReadInt32();
            m_header.EntryPoint = reader.ReadInt32();
            m_header.CommandListPosition = reader.ReadInt32();
            m_header.CommandListLength = reader.ReadInt32();
            m_header.ConstantStringPosition = reader.ReadInt32();
            m_header.ConstantStringLength = reader.ReadInt32();
            m_header.LabelListPosition = reader.ReadInt32();
            m_header.LabelListLength = reader.ReadInt32();
            m_header.SourceFileListPosition = reader.ReadInt32();
            m_header.SourceFileListLength = reader.ReadInt32();
            m_header.CommandCount = reader.ReadInt32();
            m_header.Features = reader.ReadInt32();

            // Read label list (optional)

            if (m_header.LabelListPosition > 0 && m_header.LabelListLength > 0)
            {
                input.Position = m_header.LabelListPosition;

                for (var i = 0; i < m_header.LabelListLength; i++)
                {
                    var item = new Label();

                    item.Name = encoding.GetString(reader.ReadBytes(32)).TrimEnd('\0');
                    item.CommandIndex = reader.ReadInt32();

                    m_labels.Add(item);
                }
            }

            // Read source code file list (optional)

            if ((m_header.Flags & 1) != 0)
            {
                input.Position = m_header.SourceFileListPosition;

                while (input.Position < m_header.SourceFileListPosition + m_header.SourceFileListLength)
                {
                    var name = reader.ReadNullTerminatedString(encoding);

                    // Skip padding bytes
                    if (string.IsNullOrEmpty(name))
                    {
                        break;
                    }

                    m_source_files.Add(name);
                }
            }

            // Read command list

            input.Position = m_header.CommandListPosition;

            for (var i = 0; i < m_header.CommandCount; i++)
            {
                var item = new Command();

                item.Id = reader.ReadInt32();
                item.ArgPosition = reader.ReadInt32();
                item.ArgCount = reader.ReadByte();
                item.SourceFile = reader.ReadByte();
                item.SourceLine = reader.ReadUInt16();
                item.Reserved = reader.ReadInt32(); // -1

                m_commands.Add(item);
            }

            // Read arguments

            foreach (var item in m_commands)
            {
                input.Position = m_header.CommandListPosition + item.ArgPosition;

                ResetByteReader();

                for (var i = 0; i < item.ArgCount; i++)
                {
                    var arg = ReadArgument(reader);
                    item.Args.Add(arg);
                }
            }

            // Read constant string list

            input.Position = m_header.ConstantStringPosition;

            while (input.Position < m_header.ConstantStringPosition + m_header.ConstantStringLength)
            {
                var item = new ConstantString();

                item.Position = Convert.ToInt32(input.Position - m_header.ConstantStringPosition);
                item.Value = reader.ReadNullTerminatedString(encoding);

                m_constant_strings.Add(item);

                input.AlignPosition(4);
            }

            // Done

            if (input.Position != input.Length)
            {
                Console.WriteLine($"WARNING: This script was not fully parsed.");
            }
        }

        private Argument ReadArgument(BinaryReader reader)
        {
            var item = new Argument();

            item.Code = ReadByte(reader);

            switch (item.Code & 15)
            {
                case 0:
                {
                    item.Value = 0;
                    break;
                }
                case 1:
                {
                    item.Value = 1;
                    break;
                }
                case 2:
                {
                    item.Value = -1;
                    break;
                }
                case 4:
                {
                    item.Value = ReadByte(reader);
                    break;
                }
                case 5:
                {
                    var v0 = ReadByte(reader);
                    var v1 = ReadByte(reader);
                    item.Value = (v1 << 8) | v0;
                    break;
                }
                case 6:
                {
                    var v0 = ReadByte(reader);
                    var v1 = ReadByte(reader);
                    var v2 = ReadByte(reader);
                    item.Value = (v2 << 16) | (v1 << 8) | v0;
                    break;
                }
                case 7:
                {
                    item.Value = reader.ReadInt32();
                    break;
                }
                case 8:
                {
                    var v0 = ReadByte(reader);
                    item.Value = (int)(v0 | 0xFFFFFF00);
                    break;
                }
                case 9:
                {
                    var v0 = ReadByte(reader);
                    var v1 = ReadByte(reader);
                    item.Value = (int)((v1 << 8) | v0 | 0xFFFF0000);
                    break;
                }
                case 10:
                {
                    var v0 = ReadByte(reader);
                    var v1 = ReadByte(reader);
                    var v2 = ReadByte(reader);
                    item.Value = (int)((v2 << 16) | (v1 << 8) | v0 | 0xFF000000);
                    break;
                }
                default:
                {
                    throw new InvalidDataException("Invalid opcode.");
                }
            }

            return item;
        }

        private long m_bytes_pos;
        private uint m_bytes;
        private uint m_byte_count;

        private void ResetByteReader()
        {
            m_bytes = 0;
            m_byte_count = 0;
        }

        private byte ReadByte(BinaryReader reader)
        {
            if (m_byte_count == 0)
            {
                m_bytes = reader.ReadUInt32();
                m_byte_count = 4;
            }

            var b = (byte)(m_bytes & 0xFF);

            m_bytes >>= 8;
            m_byte_count--;

            return b;
        }

        private void ResetByteWriter()
        {
            m_bytes_pos = 0;
            m_bytes = 0;
            m_byte_count = 0;
        }

        private void WriteByte(BinaryWriter writer, byte value)
        {
            var savePos = writer.BaseStream.Position;

            if (m_byte_count == 0)
            {
                m_bytes_pos = writer.BaseStream.Position;
                m_bytes = 0;
                m_byte_count = 0;
                writer.Write(0);
                savePos += 4;
            }

            m_bytes |= (uint)value << (int)(m_byte_count << 3);
            m_byte_count++;

            writer.BaseStream.Position = m_bytes_pos;
            writer.Write(m_bytes);
            writer.BaseStream.Position = savePos;

            if (m_byte_count == 4)
            {
                m_byte_count = 0;
            }
        }

        public void Save(string filePath, Encoding encoding)
        {
            using var output = File.Create(filePath);
            using var writer = new BinaryWriter(output);

            writer.Write(Signature);

            writer.WriteByte(0x20);
            writer.WriteByte(0x1A);
            writer.WriteByte(0x00);

            output.Position = 0x40;

            // Write label list

            if (m_labels.Count != 0)
            {
                m_header.LabelListPosition = Convert.ToInt32(output.Position);
                m_header.LabelListLength = m_labels.Count;

                foreach (var item in m_labels)
                {
                    var name = new byte[32];

                    var buf = encoding.GetBytes(item.Name);
                    Array.Copy(buf, name, buf.Length);

                    writer.Write(name);
                    writer.Write(item.CommandIndex);
                }
            }

            // Write source code file list

            if (m_source_files.Count != 0)
            {
                m_header.Flags |= 1;
                m_header.SourceFileListPosition = Convert.ToInt32(output.Position);

                foreach (var item in m_source_files)
                {
                    writer.WriteNullTerminatedString(item, encoding);
                }

                output.AlignPosition(4);

                m_header.SourceFileListLength = Convert.ToInt32(output.Position - m_header.SourceFileListPosition);
            }

            // Reconstruct argument and constant string section and update command list

            var argOutput = new MemoryStream(0x100000);
            var argWriter = new BinaryWriter(argOutput);

            var strOutput = new MemoryStream(0x100000);
            var strWriter = new BinaryWriter(strOutput);

            var constantStringMap = m_constant_strings.ToFrozenDictionary(x => x.Position, x => x.Value);

            m_header.CommandListLength = 16 * m_commands.Count;

            foreach (var cmd in m_commands)
            {
                cmd.ArgPosition = Convert.ToInt32(m_header.CommandListLength + argOutput.Position);

                ResetByteWriter();

                foreach (var arg in cmd.Args)
                {
                    // Update string reference
                    if ((arg.Code & 0xF0) == 0x20)
                    {
                        // Get string to encode
                        arg.RefString ??= constantStringMap[arg.Value];

                        // Get string position
                        arg.Value = Convert.ToInt32(strOutput.Position);

                        // Write string
                        strWriter.WriteNullTerminatedString(arg.RefString, encoding);
                        strOutput.AlignPosition(4);
                    }

                    WriteArgument(argWriter, arg);
                    argOutput.AlignPosition(4);
                }
            }

            // Write command list

            m_header.CommandListPosition = Convert.ToInt32(output.Position);
            m_header.CommandCount = m_commands.Count;

            foreach (var cmd in m_commands)
            {
                writer.Write(cmd.Id);
                writer.Write(cmd.ArgPosition);
                writer.Write((byte)cmd.ArgCount);
                writer.Write((byte)cmd.SourceFile);
                writer.Write((ushort)cmd.SourceLine);
                writer.Write(cmd.Reserved);
            }

            // Write arguments

            argOutput.Position = 0;
            argOutput.CopyTo(output);
            output.AlignPosition(4);

            m_header.CommandListLength = Convert.ToInt32(output.Position - m_header.CommandListPosition);

            // Write constant string list

            m_header.ConstantStringPosition = Convert.ToInt32(output.Position);

            strOutput.Position = 0;
            strOutput.CopyTo(output);

            // Align to 4 bytes
            var alignedEndPos = (output.Position + 3) & ~3;

            while (output.Position < alignedEndPos)
            {
                output.WriteByte(0);
            }

            m_header.ConstantStringLength = Convert.ToInt32(output.Position - m_header.ConstantStringPosition);

            // Write header

            output.Position = 0x10;

            writer.Write(m_header.Flags);
            writer.Write(m_header.EntryPoint);
            writer.Write(m_header.CommandListPosition);
            writer.Write(m_header.CommandListLength);
            writer.Write(m_header.ConstantStringPosition);
            writer.Write(m_header.ConstantStringLength);
            writer.Write(m_header.LabelListPosition);
            writer.Write(m_header.LabelListLength);
            writer.Write(m_header.SourceFileListPosition);
            writer.Write(m_header.SourceFileListLength);
            writer.Write(m_header.CommandCount);
            writer.Write(m_header.Features);

            // Done

            writer.Flush();
            output.Close();
        }

        private void WriteArgument(BinaryWriter writer, Argument argument)
        {
            int mode;
            var value = (uint)argument.Value;

            if (value == 0)
                mode = 0;
            else if (value == 1)
                mode = 1;
            else if (value == 0xFFFFFFFF)
                mode = 2;
            else if ((value & 0xFFFFFF00) == 0x00000000)
                mode = 4;
            else if ((value & 0xFFFF0000) == 0x00000000)
                mode = 5;
            else if ((value & 0xFF000000) == 0x00000000)
                mode = 6;
            else if ((value & 0xFFFFFF00) == 0xFFFFFF00)
                mode = 8;
            else if ((value & 0xFFFF0000) == 0xFFFF0000)
                mode = 9;
            else if ((value & 0xFF000000) == 0xFF000000)
                mode = 10;
            else
                mode = 7;

            var code = argument.Code;

            code &= ~0x0F;
            code |= mode;

            WriteByte(writer, (byte)code);

            switch (mode)
            {
                case 4:
                case 8:
                {
                    var v0 = (byte)(value & 0xFF);
                    WriteByte(writer, v0);
                    break;
                }
                case 5:
                case 9:
                {
                    var v0 = (byte)(value & 0xFF);
                    var v1 = (byte)(value >> 8);
                    WriteByte(writer, v0);
                    WriteByte(writer, v1);
                    break;
                }
                case 6:
                case 10:
                {
                    var v0 = (byte)(value & 0xFF);
                    var v1 = (byte)(value >> 8);
                    var v2 = (byte)(value >> 16);
                    WriteByte(writer, v0);
                    WriteByte(writer, v1);
                    WriteByte(writer, v2);
                    break;
                }
                case 7:
                {
                    writer.Write(value);
                    break;
                }
            }
        }

        public void ExportJson(string filePath)
        {
            var constantStringMap = m_constant_strings.ToFrozenDictionary(x => x.Position, x => x.Value);

            foreach (var cmd in m_commands)
            {
                foreach (var arg in cmd.Args)
                {
                    if ((arg.Code & 0xF0) == 0x20)
                    {
                        arg.RefString = constantStringMap[arg.Value];
                    }
                }
            }

            var obj = new ExportObject
            {
                Header = m_header,
                Labels = m_labels,
                SourceFiles = m_source_files,
                Commands = m_commands,
                ConstantStrings = m_constant_strings,
            };

            var json = JsonSerializer.Serialize(obj, JsonSourceGenerateContext.Default.ExportObject);

            File.WriteAllText(filePath, json);
        }

        public void ExportText(string filePath)
        {
            using var writer = File.CreateText(filePath);

            var constantStringMap = m_constant_strings.ToFrozenDictionary(x => x.Position, x => x.Value);

            for (var i = 0; i < m_commands.Count; i++)
            {
                var cmd = m_commands[i];

                for (var j = 0; j < cmd.Args.Count; j++)
                {
                    var arg = cmd.Args[j];

                    if ((arg.Code & 0xF0) == 0x20)
                    {
                        var text = constantStringMap[arg.Value];

                        if (string.IsNullOrWhiteSpace(text))
                        {
                            continue;
                        }

                        // 24 bits for command index
                        //  8 bits for argument index
                        var id = (i << 8) | j;

                        // Make sure the text is a single line
                        text = text.Escape();

                        writer.WriteLine("◇{0:X8}◇{1}", id, text);
                        writer.WriteLine("◆{0:X8}◆{1}", id, text);
                        writer.WriteLine();
                    }
                }
            }

            writer.Flush();
            writer.Close();
        }

        public void ImportText(string filePath)
        {
            var translation = Translation.Load(filePath);

            var constantStringMap = m_constant_strings.ToFrozenDictionary(x => x.Position, x => x.Value);

            for (var i = 0; i < m_commands.Count; i++)
            {
                var cmd = m_commands[i];

                for (var j = 0; j < cmd.Args.Count; j++)
                {
                    var arg = cmd.Args[j];

                    if ((arg.Code & 0xF0) == 0x20)
                    {
                        var id = (i << 8) | j;

                        if (translation.TryGetValue(id, out var text))
                        {
                            arg.RefString = text.Unescape();
                        }
                        else
                        {
                            arg.RefString = constantStringMap[arg.Value];
                        }
                    }
                }
            }
        }

        class Header
        {
            [JsonPropertyName("Flags")]
            public int Flags { get; set; }

            [JsonPropertyName("EntryPoint")]
            public int EntryPoint { get; set; }

            [JsonPropertyName("CommandListPos")]
            public int CommandListPosition { get; set; }

            [JsonPropertyName("CommandListLength")]
            public int CommandListLength { get; set; }

            [JsonPropertyName("StringPos")]
            public int ConstantStringPosition { get; set; }

            [JsonPropertyName("StringLength")]
            public int ConstantStringLength { get; set; }

            [JsonPropertyName("LabelListPos")]
            public int LabelListPosition { get; set; }

            [JsonPropertyName("LabelListLength")]
            public int LabelListLength { get; set; }

            [JsonPropertyName("SourceListPos")]
            public int SourceFileListPosition { get; set; }

            [JsonPropertyName("SourceListLength")]
            public int SourceFileListLength { get; set; }

            [JsonPropertyName("CommandCount")]
            public int CommandCount { get; set; }

            [JsonPropertyName("Features")]
            public int Features { get; set; }
        }

        class Label
        {
            [JsonPropertyName("Name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("Command")]
            public int CommandIndex { get; set; }
        }

        class Argument
        {
            [JsonPropertyName("Code")]
            public int Code { get; set; }

            [JsonPropertyName("Value")]
            public int Value { get; set; }

            // Helpers

            [JsonPropertyName("String")]
            public string? RefString { get; set; }
        }

        class Command
        {
            [JsonPropertyName("Id")]
            public int Id { get; set; }

            [JsonPropertyName("ArgPos")]
            public int ArgPosition { get; set; }

            [JsonPropertyName("ArgCount")]
            public int ArgCount { get; set; }

            [JsonPropertyName("File")]
            public int SourceFile { get; set; }

            [JsonPropertyName("Line")]
            public int SourceLine { get; set; }

            [JsonIgnore]
            public int Reserved { get; set; }

            // Helpers

            [JsonPropertyName("Args")]
            public List<Argument> Args { get; set; } = [];
        }

        class ConstantString
        {
            [JsonPropertyName("Pos")]
            public int Position { get; set; }

            [JsonPropertyName("Value")]
            public string Value { get; set; } = string.Empty;
        }

        class ExportObject
        {
            [JsonPropertyName("Header")]
            public Header Header { get; set; } = new();

            [JsonPropertyName("Labels")]
            public List<Label> Labels { get; set; } = [];

            [JsonPropertyName("Sources")]
            public List<string> SourceFiles { get; set; } = [];

            [JsonPropertyName("Commands")]
            public List<Command> Commands { get; set; } = [];

            [JsonPropertyName("Strings")]
            public List<ConstantString> ConstantStrings { get; set; } = [];
        }

        [JsonSerializable(typeof(ExportObject))]
        private partial class JsonSourceGenerateContext : JsonSerializerContext
        {
            static JsonSourceGenerateContext()
            {
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true,
                };

                Default = new JsonSourceGenerateContext(options);
            }
        }
    }
}
