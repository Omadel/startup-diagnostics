using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Pixels of the icon of the launch screen, read from its file as it is, whatever its import settings, in gamma
    ///     space and fitted in a square: the images of the Android build are made from them.
    /// </summary>
    internal sealed class LaunchScreenImage
    {
        // A pixel more transparent than this does not show; the smoothed edge of a disc may spill a pixel out of it.
        private const float VISIBLE_OPACITY = 0.05f;
        private const float DISC_MARGIN = 1;

        public LaunchScreenImage(Color[] pixels, int size)
        {
            Pixels = pixels;
            Size = size;
        }

        /// <summary>
        ///     The pixels row by row from the bottom, as <see cref="Texture2D.GetPixels()" /> gives them.
        /// </summary>
        public Color[] Pixels { get; }

        /// <summary>
        ///     The side of the square, in pixels.
        /// </summary>
        public int Size { get; }

        /// <summary>
        ///     The image of the file of an asset, or null with a warning if the asset has no PNG or JPEG file.
        /// </summary>
        internal static LaunchScreenImage Read(Texture2D icon, Action<string> logWarning)
        {
            var path = icon == null ? null : AssetDatabase.GetAssetPath(icon);
            var image = !string.IsNullOrEmpty(path) && File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null;
            if (image == null)
            {
                logWarning($"{StartupMeasure.LOG_PREFIX} The icon of the launch screen{(string.IsNullOrEmpty(path) ? string.Empty : $", {path},")} is not a PNG or JPEG file: the Android build has the launch screens of Unity.");
            }

            return image;
        }

        /// <summary>
        ///     The image of a PNG or JPEG file, or null if the file is neither.
        /// </summary>
        internal static LaunchScreenImage Decode(byte[] file)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                return texture.LoadImage(file) ? FitInSquare(texture.GetPixels(), texture.width, texture.height) : null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        ///     The image at the centre of a transparent square of its larger side.
        /// </summary>
        internal static LaunchScreenImage FitInSquare(Color[] pixels, int width, int height)
        {
            if (width == height) return new LaunchScreenImage(pixels, width);

            var size = Math.Max(width, height);
            var square = new Color[size * size];
            var left = (size - width) / 2;
            var bottom = (size - height) / 2;
            for (var y = 0; y < height; y++)
            {
                Array.Copy(pixels, y * width, square, (bottom + y) * size + left, width);
            }

            return new LaunchScreenImage(square, size);
        }

        /// <summary>
        ///     The image at its size at the centre of a larger transparent square.
        /// </summary>
        internal LaunchScreenImage Pad(int size)
        {
            if (size <= Size) return this;

            var padded = new Color[size * size];
            var offset = (size - Size) / 2;
            for (var y = 0; y < Size; y++)
            {
                Array.Copy(Pixels, y * Size, padded, (offset + y) * size + offset, Size);
            }

            return new LaunchScreenImage(padded, size);
        }

        /// <summary>
        ///     The image at another size. Smaller, each pixel averages the pixels it covers, weighted by the area covered and,
        ///     for the colour, by their opacity: transparent pixels do not darken the edges. Larger, each pixel is
        ///     interpolated between the four nearest, their colours weighted by their opacity.
        /// </summary>
        internal LaunchScreenImage Resize(int size)
        {
            if (size == Size) return this;

            return size < Size ? Shrink(size) : Enlarge(size);
        }

        /// <summary>
        ///     Opaque pixels of the image over the background colour, blended in gamma space as Android does.
        /// </summary>
        internal Color[] Flatten(Color background)
        {
            var flat = new Color[Pixels.Length];
            for (var i = 0; i < Pixels.Length; i++)
            {
                var color = Color.Lerp(background, Pixels[i], Pixels[i].a);
                color.a = 1;
                flat[i] = color;
            }

            return flat;
        }

        /// <summary>
        ///     Whether visible pixels lie outside the disc inscribed in the image, which the splash screen of Android 12 and
        ///     later crops away.
        /// </summary>
        internal bool HasVisiblePixelsOutsideDisc()
        {
            var radius = Size / 2f;
            var limit = (radius + DISC_MARGIN) * (radius + DISC_MARGIN);
            for (var y = 0; y < Size; y++)
            {
                var dy = y + 0.5f - radius;
                for (var x = 0; x < Size; x++)
                {
                    var dx = x + 0.5f - radius;
                    if (dx * dx + dy * dy > limit && Pixels[y * Size + x].a > VISIBLE_OPACITY) return true;
                }
            }

            return false;
        }

        internal byte[] EncodeToPng()
        {
            return Encode(Pixels, TextureFormat.RGBA32, ImageConversion.EncodeToPNG);
        }

        /// <summary>
        ///     A JPEG file of the image over the background colour.
        /// </summary>
        internal byte[] EncodeToJpg(Color background, int quality)
        {
            return Encode(Flatten(background), TextureFormat.RGB24, texture => texture.EncodeToJPG(quality));
        }

        private byte[] Encode(Color[] pixels, TextureFormat format, Func<Texture2D, byte[]> encode)
        {
            var texture = new Texture2D(Size, Size, format, false, false);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                return encode(texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private LaunchScreenImage Shrink(int size)
        {
            var scale = (float)Size / size;
            var result = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                var top = y * scale;
                var bottom = (y + 1) * scale;
                for (var x = 0; x < size; x++)
                {
                    var left = x * scale;
                    var right = (x + 1) * scale;
                    var sum = Color.clear;
                    var alpha = 0f;
                    for (var sourceY = (int)top; sourceY < Mathf.Min(Mathf.CeilToInt(bottom), Size); sourceY++)
                    {
                        var height = Mathf.Min(sourceY + 1, bottom) - Mathf.Max(sourceY, top);
                        for (var sourceX = (int)left; sourceX < Mathf.Min(Mathf.CeilToInt(right), Size); sourceX++)
                        {
                            var pixel = Pixels[sourceY * Size + sourceX];
                            var weight = (Mathf.Min(sourceX + 1, right) - Mathf.Max(sourceX, left)) * height * pixel.a;
                            sum += pixel * weight;
                            alpha += weight;
                        }
                    }

                    result[y * size + x] = alpha > 0 ? new Color(sum.r / alpha, sum.g / alpha, sum.b / alpha, alpha / (scale * scale)) : Color.clear;
                }
            }

            return new LaunchScreenImage(result, size);
        }

        private LaunchScreenImage Enlarge(int size)
        {
            var scale = (float)Size / size;
            var result = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                var sourceY = (y + 0.5f) * scale - 0.5f;
                var bottom = Mathf.FloorToInt(sourceY);
                for (var x = 0; x < size; x++)
                {
                    var sourceX = (x + 0.5f) * scale - 0.5f;
                    var left = Mathf.FloorToInt(sourceX);
                    var lower = Color.LerpUnclamped(GetPremultiplied(left, bottom), GetPremultiplied(left + 1, bottom), sourceX - left);
                    var upper = Color.LerpUnclamped(GetPremultiplied(left, bottom + 1), GetPremultiplied(left + 1, bottom + 1), sourceX - left);
                    var pixel = Color.LerpUnclamped(lower, upper, sourceY - bottom);
                    result[y * size + x] = pixel.a > 0 ? new Color(pixel.r / pixel.a, pixel.g / pixel.a, pixel.b / pixel.a, pixel.a) : Color.clear;
                }
            }

            return new LaunchScreenImage(result, size);
        }

        /// <summary>
        ///     A pixel with its colour multiplied by its opacity, edges clamped.
        /// </summary>
        private Color GetPremultiplied(int x, int y)
        {
            var pixel = Pixels[Mathf.Clamp(y, 0, Size - 1) * Size + Mathf.Clamp(x, 0, Size - 1)];
            return new Color(pixel.r * pixel.a, pixel.g * pixel.a, pixel.b * pixel.a, pixel.a);
        }
    }
}