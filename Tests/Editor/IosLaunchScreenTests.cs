using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class IosLaunchScreenTests
    {
        private static readonly Color _background = new(0.13725491f, 0.12156863f, 0.1254902f, 1);

        private FakeIosPlayerSettings _settings;
        private Texture2D _icon;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<FakeIosPlayerSettings>();
            _icon = new Texture2D(4, 4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_icon);
        }

        [Test]
        public void Apply_OtherLaunchScreen_SetsEveryPropertyAndNamesThem()
        {
            var changed = IosLaunchScreen.Apply(new SerializedObject(_settings), _icon, _background, out var missing);

            Assert.That(changed, Has.Count.EqualTo(9));
            Assert.That(missing, Is.Empty);
            Assert.That(_settings.IphoneType, Is.EqualTo((int)iOSLaunchScreenType.ImageAndBackgroundConstant));
            Assert.That(_settings.PortraitImage, Is.SameAs(_icon));
            Assert.That(IosLaunchScreen.IsSameColor(_settings.IpadBackground, _background), Is.True);
            Assert.That(_settings.IpadSize, Is.EqualTo(LaunchScreen.ICON_SIZE));
        }

        [Test]
        public void Apply_SameLaunchScreen_ChangesNothing()
        {
            IosLaunchScreen.Apply(new SerializedObject(_settings), _icon, _background, out _);

            var changed = IosLaunchScreen.Apply(new SerializedObject(_settings), _icon, _background, out _);

            Assert.That(changed, Is.Empty);
        }

        [Test]
        public void Apply_NoSuchProperties_NamesThemAsMissing()
        {
            var changed = IosLaunchScreen.Apply(new SerializedObject(_icon), _icon, _background, out var missing);

            Assert.That(changed, Is.Empty);
            Assert.That(missing, Does.Contain(IosLaunchScreen.IPHONE_TYPE).And.Contain(IosLaunchScreen.IPAD_SIZE));
        }

        [Test]
        public void IsSameColor_DifferenceBelowOneStep_IsTheSame()
        {
            Assert.That(IosLaunchScreen.IsSameColor(_background, _background + new Color(0.001f, 0, 0, 0)), Is.True);
            Assert.That(IosLaunchScreen.IsSameColor(_background, _background + new Color(0.01f, 0, 0, 0)), Is.False);
        }
    }
}