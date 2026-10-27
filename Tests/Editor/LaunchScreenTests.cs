using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class LaunchScreenTests
    {
        [TestCase(true, false, ExpectedResult = true)]
        [TestCase(false, false, ExpectedResult = false)]
        [TestCase(true, true, ExpectedResult = false)]
        [TestCase(false, true, ExpectedResult = false)]
        public bool IsShown_EndDeferredAndRequested_OnlyWhileTheStartGoesOn(bool isEndDeferred, bool isEndRequested)
        {
            return LaunchScreen.IsShown(isEndDeferred, isEndRequested);
        }

        [Test]
        public void FitInSquare_Square_TakesTheWholeSquare()
        {
            Assert.That(LaunchScreen.FitInSquare(512, 512, 192), Is.EqualTo(new Vector2(192, 192)));
        }

        [Test]
        public void FitInSquare_Wide_KeepsItsProportionsInTheWidth()
        {
            Assert.That(LaunchScreen.FitInSquare(400, 200, 192), Is.EqualTo(new Vector2(192, 96)));
        }

        [Test]
        public void FitInSquare_Tall_KeepsItsProportionsInTheHeight()
        {
            Assert.That(LaunchScreen.FitInSquare(100, 400, 192), Is.EqualTo(new Vector2(48, 192)));
        }

        [Test]
        public void GetLoadingRingOffset_Sizes_IsFromTheCentreOfTheIconToTheCentreOfTheRing()
        {
            Assert.That(LaunchScreen.GetLoadingRingOffset(192, 28, 40), Is.EqualTo(150));
        }

        [TestCase(0f, 0.3f, ExpectedResult = 0f)]
        [TestCase(0.15f, 0.3f, ExpectedResult = 0.5f)]
        [TestCase(1f, 0.3f, ExpectedResult = 1f)]
        [TestCase(0.01f, 0f, ExpectedResult = 1f)]
        [TestCase(0f, 0f, ExpectedResult = 0f)]
        public float GetLoadingRingOpacity_Elapsed_HiddenOnTheFirstFrameThenFadingIn(float elapsed, float duration)
        {
            return LaunchScreen.GetLoadingRingOpacity(elapsed, duration);
        }

        [Test]
        public void FindFreeLayer_LastLayersNamed_IsTheLastUnnamedOne()
        {
            var layer = LaunchScreen.FindFreeLayer(index => index >= 30 ? "Named" : string.Empty);

            Assert.That(layer, Is.EqualTo(29));
        }

        [Test]
        public void FindFreeLayer_AllNamed_IsTheLastLayer()
        {
            Assert.That(LaunchScreen.FindFreeLayer(_ => "Named"), Is.EqualTo(31));
        }

        [Test]
        public void FindFreeLayer_OnlyBuiltInLayersUnnamed_LeavesThemOut()
        {
            var layer = LaunchScreen.FindFreeLayer(index => index >= 8 ? "Named" : string.Empty);

            Assert.That(layer, Is.EqualTo(31));
        }
    }
}