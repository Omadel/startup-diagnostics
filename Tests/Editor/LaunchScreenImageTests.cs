using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class LaunchScreenImageTests
    {
        private static readonly Color _red = new(1, 0, 0, 1);
        private static readonly Color _background = new(0.2f, 0.4f, 0.6f, 1);

        [Test]
        public void FitInSquare_Wide_CentresItInATransparentSquare()
        {
            var pixels = Enumerable.Repeat(_red, 4 * 2).ToArray();

            var image = LaunchScreenImage.FitInSquare(pixels, 4, 2);

            Assert.That(image.Size, Is.EqualTo(4));
            Assert.That(image.Pixels[0], Is.EqualTo(Color.clear));
            Assert.That(image.Pixels[1 * 4 + 0], Is.EqualTo(_red));
            Assert.That(image.Pixels[2 * 4 + 3], Is.EqualTo(_red));
            Assert.That(image.Pixels[3 * 4 + 3], Is.EqualTo(Color.clear));
        }

        [Test]
        public void Pad_LargerSquare_CentresTheImage()
        {
            var image = Uniform(_red, 4);

            var padded = image.Pad(6);

            Assert.That(padded.Size, Is.EqualTo(6));
            Assert.That(padded.Pixels[0], Is.EqualTo(Color.clear));
            Assert.That(padded.Pixels[1 * 6 + 1], Is.EqualTo(_red));
            Assert.That(padded.Pixels[4 * 6 + 4], Is.EqualTo(_red));
            Assert.That(padded.Pixels[5 * 6 + 5], Is.EqualTo(Color.clear));
        }

        [TestCase(16, 7)]
        [TestCase(7, 16)]
        public void Resize_Uniform_KeepsItsColour(int size, int newSize)
        {
            var image = Uniform(_red, size);

            var resized = image.Resize(newSize);

            Assert.That(resized.Size, Is.EqualTo(newSize));
            Assert.That(resized.Pixels.All(pixel => Mathf.Abs(pixel.r - 1) < 1e-4f && pixel.g < 1e-4f && Mathf.Abs(pixel.a - 1) < 1e-4f), Is.True);
        }

        [Test]
        public void Resize_Smaller_TransparentPixelsDoNotDarkenTheColour()
        {
            var pixels = new[] { _red, Color.clear, Color.clear, Color.clear };

            var resized = new LaunchScreenImage(pixels, 2).Resize(1);

            Assert.That(resized.Pixels[0].r, Is.EqualTo(1).Within(1e-4f));
            Assert.That(resized.Pixels[0].a, Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void Flatten_HalfTransparent_BlendsOverTheBackgroundOpaquely()
        {
            var image = new LaunchScreenImage(new[] { new Color(1, 1, 1, 0.5f) }, 1);

            var flat = image.Flatten(_background);

            Assert.That(flat[0].r, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(flat[0].b, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(flat[0].a, Is.EqualTo(1));
        }

        [Test]
        public void HasVisiblePixelsOutsideDisc_Disc_IsFalse()
        {
            Assert.That(Disc(64).HasVisiblePixelsOutsideDisc(), Is.False);
        }

        [Test]
        public void HasVisiblePixelsOutsideDisc_OpaqueSquare_IsTrue()
        {
            Assert.That(Uniform(_red, 64).HasVisiblePixelsOutsideDisc(), Is.True);
        }

        [Test]
        public void Decode_Png_GivesItsPixels()
        {
            var png = Uniform(_red, 8).EncodeToPng();

            var image = LaunchScreenImage.Decode(png);

            Assert.That(image.Size, Is.EqualTo(8));
            Assert.That(image.Pixels.All(pixel => pixel == _red), Is.True);
        }

        [Test]
        public void Decode_NotAnImage_IsNull()
        {
            Assert.That(LaunchScreenImage.Decode(new byte[] { 1, 2, 3 }), Is.Null);
        }

        [Test]
        public void EncodeToJpg_Transparent_IsTheBackground()
        {
            var jpg = Uniform(Color.clear, 8).EncodeToJpg(_background, 100);

            var image = LaunchScreenImage.Decode(jpg);

            Assert.That(image.Pixels[0].g, Is.EqualTo(_background.g).Within(0.02f));
            Assert.That(image.Pixels[0].a, Is.EqualTo(1));
        }

        private static LaunchScreenImage Uniform(Color color, int size)
        {
            return new LaunchScreenImage(Enumerable.Repeat(color, size * size).ToArray(), size);
        }

        /// <summary>
        ///     An opaque disc inscribed in a transparent square, its edge one pixel inside.
        /// </summary>
        private static LaunchScreenImage Disc(int size)
        {
            var pixels = new Color[size * size];
            var radius = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - radius;
                    var dy = y + 0.5f - radius;
                    pixels[y * size + x] = dx * dx + dy * dy < (radius - 1) * (radius - 1) ? _red : Color.clear;
                }
            }

            return new LaunchScreenImage(pixels, size);
        }
    }
}