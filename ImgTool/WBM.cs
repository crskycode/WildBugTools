using CommonLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace ImgTool
{
    internal class WBM
    {
        public static void Extract(string filePath, string outputPath)
        {
            using var wpx = new WpxReader(filePath, "BMP");

            if (!wpx.Contains(16))
            {
                throw new Exception("BMP information not found.");
            }

            var info = wpx.Read(16);

            var width = BitConverter.ToInt16(info, 4);
            var height = BitConverter.ToInt16(info, 6);
            var bpp = info[12];

            int base_len;
            int stride;

            switch (bpp)
            {
                case 1:
                    base_len = 1;
                    stride = (width + 7) >> 3;
                    break;
                case 4:
                    base_len = 1;
                    stride = (width + 1) >> 1;
                    break;
                case 8:
                    base_len = 1;
                    stride = width;
                    break;
                case 16:
                    base_len = 2;
                    stride = 2 * width;
                    break;
                case 24:
                    base_len = 3;
                    stride = 3 * width;
                    break;
                case 32:
                    base_len = 4;
                    stride  = 4 * width;
                    break;
                default:
                    throw new Exception("Not supported format.");
            }

            stride = (stride + 3) & ~3;

            if (!wpx.Contains(17))
            {
                throw new Exception("Image data not found.");
            }

            var pixels = wpx.ReadImage(17, base_len, stride);

            if (bpp < 24)
            {
                throw new NotImplementedException("Not supported format.");
            }

            if (wpx.Contains(19))
            {
                var alpha_stride = (width + 3) & ~3;
                var alpha_image = wpx.ReadImage(19, 1, alpha_stride);

                if (bpp == 24)
                {
                    var buffer = new byte[4 * width * height];

                    for (var y = 0; y < height; y++)
                    {
                        var src = y * stride;
                        var asrc = y * alpha_stride;
                        var dst = 4 * y * width;

                        for (var x = 0; x < width; x++)
                        {
                            buffer[dst] = pixels[src];
                            buffer[dst + 1] = pixels[src + 1];
                            buffer[dst + 2] = pixels[src + 2];
                            buffer[dst + 3] = alpha_image[asrc];
                            src += 3;
                            asrc += 1;
                            dst += 4;
                        }
                    }

                    var image = Image.LoadPixelData<Bgra32>(buffer, width, height);
                    image.SaveAsPng(outputPath);
                    image.Dispose();
                }
                else if (bpp == 32)
                {
                    var buffer = new byte[4 * width * height];

                    for (var y = 0; y < height; y++)
                    {
                        var src = y * stride;
                        var asrc = y * alpha_stride;
                        var dst = 4 * y * width;

                        for (var x = 0; x < width; x++)
                        {
                            buffer[dst] = pixels[src];
                            buffer[dst + 1] = pixels[src + 1];
                            buffer[dst + 2] = pixels[src + 2];
                            buffer[dst + 3] = alpha_image[asrc];
                            src += 4;
                            asrc += 1;
                            dst += 4;
                        }
                    }

                    var image = Image.LoadPixelData<Bgra32>(pixels, width, height);
                    image.SaveAsPng(outputPath);
                    image.Dispose();
                }
            }
            else
            {
                if (bpp == 24)
                {
                    var buffer = new byte[3 * width * height];

                    for (var y = 0; y < height; y++)
                    {
                        var src = y * stride;
                        var dst = 3 * y * width;

                        for (var x = 0; x < width; x++)
                        {
                            buffer[dst] = pixels[src];
                            buffer[dst + 1] = pixels[src + 1];
                            buffer[dst + 2] = pixels[src + 2];
                            src += 3;
                            dst += 3;
                        }
                    }

                    var image = Image.LoadPixelData<Bgr24>(buffer, width, height);
                    image.SaveAsPng(outputPath);
                    image.Dispose();
                }
                else if (bpp == 32)
                {
                    var image = Image.LoadPixelData<Bgra32>(pixels, width, height);
                    image.SaveAsPng(outputPath);
                    image.Dispose();
                }
            }

            wpx.Dispose();
        }

        public static void Create(string filePath, string sourcePath, string outputPath)
        {
            var wpx = new WpxReader(filePath, "BMP");

            if (!wpx.Contains(16))
            {
                throw new Exception("BMP information not found.");
            }

            const int bmpInfoId = 16;
            const int bmpPixelId = 17;
            const int bmpAlphaId = 19;

            var info = wpx.Read(bmpInfoId);

            var width = BitConverter.ToInt16(info, 4);
            var height = BitConverter.ToInt16(info, 6);
            var bpp = info[12];

            if (!wpx.Contains(bmpPixelId))
            {
                throw new Exception("Image data not found.");
            }

            using var source = Image.Load(sourcePath);

            if (source.Width != width || source.Height != height)
            {
                throw new Exception("Image size mismatch.");
            }

            var writer = new WpxWriter("BMP");

            // Add image information
            writer.AddEntry(bmpInfoId, info);

            var image = source.CloneAs<Bgra32>();

            if (wpx.Contains(bmpAlphaId))
            {
                if (bpp == 24)
                {
                    var rgb_stride = (3 * width + 3) & ~3;
                    var alpha_stride = (width + 3) & ~3;

                    var rgb_buffer = new byte [height * rgb_stride];
                    var alpha_buffer = new byte[height * alpha_stride];

                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);

                            var rgb_p = y * rgb_stride;
                            var alpha_p = y * alpha_stride;

                            for (var x = 0; x <  accessor.Width; x++)
                            {
                                rgb_buffer[rgb_p] = row[x].B;
                                rgb_buffer[rgb_p + 1] = row[x].G;
                                rgb_buffer[rgb_p + 2] = row[x].R;
                                rgb_p += 3;

                                alpha_buffer[alpha_p] = row[x].A;
                                alpha_p += 1;
                            }
                        }
                    });

                    // Add RGB
                    writer.AddEntry(bmpPixelId, rgb_buffer);

                    // Add Alpha
                    writer.AddEntry(bmpAlphaId, alpha_buffer);
                }
                else if (bpp == 32)
                {
                    var rgb_stride = (4 * width + 3) & ~3;
                    var alpha_stride = (width + 3) & ~3;

                    var rgb_buffer = new byte[height * rgb_stride];
                    var alpha_buffer = new byte[height * alpha_stride];

                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);

                            var rgb_p = y * rgb_stride;
                            var alpha_p = y * alpha_stride;

                            for (var x = 0; x < accessor.Width; x++)
                            {
                                rgb_buffer[rgb_p] = row[x].B;
                                rgb_buffer[rgb_p + 1] = row[x].G;
                                rgb_buffer[rgb_p + 2] = row[x].R;
                                rgb_buffer[rgb_p + 3] = row[x].A;
                                rgb_p += 4;

                                alpha_buffer[alpha_p] = row[x].A;
                                alpha_p += 1;
                            }
                        }
                    });

                    // Add RGB
                    writer.AddEntry(bmpPixelId, rgb_buffer);

                    // Add Alpha
                    writer.AddEntry(bmpAlphaId, alpha_buffer);
                }
            }
            else
            {
                if (bpp == 24)
                {
                    var stride = (3 * width + 3) & ~3;
                    var buffer = new byte[height * stride];

                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            var dst = y * stride;

                            for (var x = 0; x < accessor.Width; x++)
                            {
                                buffer[dst] = row[x].B;
                                buffer[dst + 1] = row[x].G;
                                buffer[dst + 2] = row[x].R;
                                dst += 3;
                            }
                        }
                    });

                    // Add RGB
                    writer.AddEntry(bmpPixelId, buffer);
                }
                else if (bpp == 32)
                {
                    var stride = (4 * width + 3) & ~3;
                    var buffer = new byte[height * stride];

                    image.ProcessPixelRows(accessor =>
                    {
                        for (var y = 0; y < accessor.Height; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            var dst = y * stride;

                            for (var x = 0; x < accessor.Width; x++)
                            {
                                buffer[dst] = row[x].B;
                                buffer[dst + 1] = row[x].G;
                                buffer[dst + 2] = row[x].R;
                                buffer[dst + 3] = row[x].A;
                                dst += 4;
                            }
                        }
                    });

                    // Add RGB
                    writer.AddEntry(bmpPixelId, buffer);
                }
            }

            writer.Save(outputPath);
        }
    }
}
