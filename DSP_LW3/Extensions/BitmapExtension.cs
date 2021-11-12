using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DSP_LW3.Extensions
{
    public static class BitmapExtension
    {
        public static Bitmap CopyToSquareCanvas(this Bitmap sourceBitmap, int canvasWidthLenght)
        {
            int maxSide = sourceBitmap.Width > sourceBitmap.Height ?
                          sourceBitmap.Width : sourceBitmap.Height;

            float ratio = maxSide / (float)canvasWidthLenght;

            Bitmap bitmapResult = sourceBitmap.Width > sourceBitmap.Height ?
                                    new Bitmap(canvasWidthLenght, (int)(sourceBitmap.Height / ratio))
                                    : new Bitmap((int)(sourceBitmap.Width / ratio), canvasWidthLenght);

            using (Graphics graphicsResult = Graphics.FromImage(bitmapResult))
            {
                graphicsResult.CompositingQuality = CompositingQuality.HighQuality;
                graphicsResult.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphicsResult.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphicsResult.DrawImage(sourceBitmap,
                                        new Rectangle(0, 0,
                                            bitmapResult.Width, bitmapResult.Height),
                                        new Rectangle(0, 0,
                                            sourceBitmap.Width, sourceBitmap.Height),
                                            GraphicsUnit.Pixel);
                graphicsResult.Flush();
            }

            return bitmapResult;
        }

        private static Bitmap ConvolutionFilter(Bitmap sourceBitmap, double[,] filterMatrix, double factor = 1, int bias = 0)
        {
            BitmapData sourceData = sourceBitmap.LockBits(new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                                                            ImageLockMode.ReadOnly,
                                                            PixelFormat.Format32bppArgb);

            byte[] pixelBuffer = new byte[sourceData.Stride * sourceData.Height];
            byte[] resultBuffer = new byte[sourceData.Stride * sourceData.Height];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            int filterWidth = filterMatrix.GetLength(1);
            int filterOffset = (filterWidth - 1) / 2;
            int offsetY = 0;
            int bitmapHeight = sourceBitmap.Height;
            int bitmapWidth = sourceBitmap.Width;
            Parallel.For(offsetY, bitmapHeight, offsetY =>
            {
                for (int offsetX = 0; offsetX < bitmapWidth; offsetX++)
                {
                    double blue = 0;
                    double green = 0;
                    double red = 0;
                    int byteOffset = offsetY * sourceData.Stride + offsetX * 4;
                    for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                    {
                        for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                        {
                            int calcOffset = byteOffset + (filterX * 4) + (filterY * sourceData.Stride);
                            if (filterY + offsetY < 0)
                            {
                                calcOffset += filterOffset * sourceData.Stride;
                            }
                            if (filterY + offsetY >= bitmapHeight)
                            {
                                calcOffset -= filterOffset * sourceData.Stride;
                            }

                            if (filterX + offsetX < 0)
                            {
                                calcOffset += filterOffset * 4;
                            }
                            if (filterX + offsetX >= bitmapWidth)
                            {
                                calcOffset -= filterOffset * 4;
                            }

                            blue += pixelBuffer[calcOffset] * filterMatrix[filterY + filterOffset, filterX + filterOffset];
                            green += pixelBuffer[calcOffset + 1] * filterMatrix[filterY + filterOffset, filterX + filterOffset];
                            red += pixelBuffer[calcOffset + 2] * filterMatrix[filterY + filterOffset, filterX + filterOffset];
                        }
                    }

                    blue = factor * blue + bias;
                    green = factor * green + bias;
                    red = factor * red + bias;

                    if (blue > 255)
                    {
                        blue = 255;
                    }
                    else if (blue < 0)
                    {
                        blue = 0;
                    }

                    if (green > 255)
                    {
                        green = 255;
                    }
                    else if (green < 0)
                    {
                        green = 0;
                    }

                    if (red > 255)
                    {
                        red = 255;
                    }
                    else if (red < 0)
                    {
                        red = 0;
                    }

                    resultBuffer[byteOffset] = (byte)blue;
                    resultBuffer[byteOffset + 1] = (byte)green;
                    resultBuffer[byteOffset + 2] = (byte)red;
                    resultBuffer[byteOffset + 3] = 255;
                }
            });

            Bitmap resultBitmap = new(bitmapWidth, bitmapHeight);

            BitmapData resultData = resultBitmap.LockBits(new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                                                            ImageLockMode.WriteOnly,
                                                            PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }

        public static Bitmap ConvolutionFilter(this Bitmap sourceBitmap, double[,] xFilterMatrix, double[,] yFilterMatrix, bool grayscale = false)
        {
            BitmapData sourceData = sourceBitmap.LockBits(new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                                                            ImageLockMode.ReadOnly,
                                                            PixelFormat.Format32bppArgb);

            byte[] pixelBuffer = new byte[sourceData.Stride * sourceData.Height];
            byte[] resultBuffer = new byte[sourceData.Stride * sourceData.Height];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            if (grayscale)
            {
                for (int k = 0; k < pixelBuffer.Length; k += 4)
                {
                    float rgb = pixelBuffer[k] * 0.11f;
                    rgb += pixelBuffer[k + 1] * 0.59f;
                    rgb += pixelBuffer[k + 2] * 0.3f;

                    pixelBuffer[k] = (byte)rgb;
                    pixelBuffer[k + 1] = pixelBuffer[k];
                    pixelBuffer[k + 2] = pixelBuffer[k];
                    pixelBuffer[k + 3] = 255;
                }
            }

            int filterWidth = xFilterMatrix.GetLength(1);
            int filterOffset = (filterWidth - 1) / 2;
            int offsetY = 0;
            int bitmapHeight = sourceBitmap.Height;
            int bitmapWidth = sourceBitmap.Width;
            Parallel.For(offsetY, bitmapHeight, offsetY =>
            {
                for (int offsetX = 0; offsetX < bitmapWidth; offsetX++)
                {
                    double blueX = 0, greenX = 0, redX = 0;
                    double blueY = 0, greenY = 0, redY = 0;

                    int byteOffset = offsetY * sourceData.Stride + offsetX * 4;
                    for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                    {
                        for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                        {
                            int calcOffset = byteOffset + (filterX * 4) + (filterY * sourceData.Stride);
                            if (filterY + offsetY < 0)
                            {
                                calcOffset += filterOffset * sourceData.Stride;
                            }
                            if (filterY + offsetY >= bitmapHeight)
                            {
                                calcOffset -= filterOffset * sourceData.Stride;
                            }

                            if (filterX + offsetX < 0)
                            {
                                calcOffset += filterOffset * 4;
                            }
                            if (filterX + offsetX >= bitmapWidth)
                            {
                                calcOffset -= filterOffset * 4;
                            }

                            blueX += pixelBuffer[calcOffset] * xFilterMatrix[filterY + filterOffset, filterX + filterOffset];
                            greenX += pixelBuffer[calcOffset + 1] * xFilterMatrix[filterY + filterOffset, filterX + filterOffset];
                            redX += pixelBuffer[calcOffset + 2] * xFilterMatrix[filterY + filterOffset, filterX + filterOffset];

                            blueY += pixelBuffer[calcOffset] * yFilterMatrix[filterY + filterOffset, filterX + filterOffset];
                            greenY += pixelBuffer[calcOffset + 1] * yFilterMatrix[filterY + filterOffset, filterX + filterOffset];
                            redY += pixelBuffer[calcOffset + 2] * yFilterMatrix[filterY + filterOffset, filterX + filterOffset];
                        }
                    }

                    double blueTotal = Math.Sqrt((blueX * blueX) + (blueY * blueY));
                    double greenTotal = Math.Sqrt((greenX * greenX) + (greenY * greenY));
                    double redTotal = Math.Sqrt((redX * redX) + (redY * redY));

                    if (blueTotal > 255)
                    {
                        blueTotal = 255;
                    }
                    else if (blueTotal < 0)
                    {
                        blueTotal = 0;
                    }

                    if (greenTotal > 255)
                    {
                        greenTotal = 255;
                    }
                    else if (greenTotal < 0)
                    {
                        greenTotal = 0;
                    }

                    if (redTotal > 255)
                    {
                        redTotal = 255;
                    }
                    else if (redTotal < 0)
                    {
                        redTotal = 0;
                    }

                    resultBuffer[byteOffset] = (byte)(blueTotal);
                    resultBuffer[byteOffset + 1] = (byte)(greenTotal);
                    resultBuffer[byteOffset + 2] = (byte)(redTotal);
                    resultBuffer[byteOffset + 3] = 255;
                }
            });

            Bitmap resultBitmap = new(bitmapWidth, bitmapHeight);
            BitmapData resultData = resultBitmap.LockBits(new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                                                            ImageLockMode.WriteOnly,
                                                            PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }

        public static Bitmap MedianFilter(this Bitmap sourceBitmap, int matrixSize)
        {
            BitmapData sourceData = sourceBitmap.LockBits(new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height),
                                                            ImageLockMode.ReadOnly,
                                                            PixelFormat.Format32bppArgb);

            byte[] pixelBuffer = new byte[sourceData.Stride * sourceData.Height];
            byte[] resultBuffer = new byte[sourceData.Stride * sourceData.Height];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            int filterOffset = (matrixSize - 1) / 2;
            List<uint> neighbourPixels = new();
            byte[] middlePixel;
            int bitmapHeight = sourceBitmap.Height;
            int bitmapWidth = sourceBitmap.Width;
            for (int offsetY = 0; offsetY < bitmapHeight; offsetY++)
            {
                for (int offsetX = 0; offsetX < bitmapWidth; offsetX++)
                {
                    int byteOffset = offsetY * sourceData.Stride + offsetX * 4;
                    neighbourPixels.Clear();
                    for (int filterY = -filterOffset; filterY <= filterOffset; filterY++)
                    {
                        for (int filterX = -filterOffset; filterX <= filterOffset; filterX++)
                        {
                            int calcOffset = byteOffset + (filterX * 4) + (filterY * sourceData.Stride);
                            if (filterY + offsetY < 0)
                            {
                                calcOffset += filterOffset * sourceData.Stride;
                            }
                            if (filterY + offsetY >= bitmapHeight)
                            {
                                calcOffset -= filterOffset * sourceData.Stride;
                            }

                            if (filterX + offsetX < 0)
                            {
                                calcOffset += filterOffset * 4;
                            }
                            if (filterX + offsetX >= bitmapWidth)
                            {
                                calcOffset -= filterOffset * 4;
                            }

                            neighbourPixels.Add(BitConverter.ToUInt32(pixelBuffer, calcOffset));
                        }
                    }

                    neighbourPixels.Sort();
                    middlePixel = BitConverter.GetBytes(neighbourPixels[neighbourPixels.Count / 2]);

                    resultBuffer[byteOffset] = middlePixel[0];
                    resultBuffer[byteOffset + 1] = middlePixel[1];
                    resultBuffer[byteOffset + 2] = middlePixel[2];
                    resultBuffer[byteOffset + 3] = middlePixel[3];
                }
            }

            Bitmap resultBitmap = new(bitmapWidth, bitmapHeight);
            BitmapData resultData = resultBitmap.LockBits(new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                                                            ImageLockMode.WriteOnly,
                                                            PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }

        public static Bitmap GaussianBlurFilter(this Bitmap sourceBitmap, double sigma, int size = 3)
        {
            (double[,] Matrix, double NormalizationRate) tuple = Matrix.GetGaussianMatrix(sigma, size);
            Bitmap resultBitmap = ConvolutionFilter(sourceBitmap, tuple.Matrix, tuple.NormalizationRate);

            return resultBitmap;
        }

        public static Bitmap BoxBlurFilter(this Bitmap sourceBitmap, int size = 3)
        {
            (double[,] Matrix, double NormalizationRate) tuple = Matrix.GetBoxBlurMatrix(size);
            Bitmap resultBitmap = ConvolutionFilter(sourceBitmap, tuple.Matrix, tuple.NormalizationRate);

            return resultBitmap;
        }

        public static Bitmap MotionBlurFilter(this Bitmap sourceBitmap, int size = 3)
        {
            (double[,] Matrix, double NormalizationRate) tuple = Matrix.GetMotionBlurMatrix(size);
            Bitmap resultBitmap = ConvolutionFilter(sourceBitmap, tuple.Matrix, tuple.NormalizationRate);

            return resultBitmap;
        }

        public static Bitmap Sobel3x3Filter(this Bitmap sourceBitmap, bool grayscale = true)
        {
            Bitmap resultBitmap = ConvolutionFilter(sourceBitmap, Matrix.Sobel3x3Horizontal, Matrix.Sobel3x3Vertical, grayscale);

            return resultBitmap;
        }

        public static Bitmap Sharpen3x3Filter(this Bitmap sourceBitmap)
        {
            Bitmap resultBitmap = ConvolutionFilter(sourceBitmap, Matrix.Sharpen3x3);

            return resultBitmap;
        }
    }
}
