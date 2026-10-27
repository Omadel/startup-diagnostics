using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AddressablesStateReaderTests
    {
        [TestCase("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android")]
        [TestCase("{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]")]
        public void IsLocalLoadPath_RuntimePath_IsTrue(string loadPath)
        {
            Assert.That(AddressablesStateReader.IsLocalLoadPath(loadPath), Is.True);
        }

        [TestCase("https://cdn.example.com/content/Android")]
        [TestCase("")]
        [TestCase(null)]
        public void IsLocalLoadPath_ServerOrNothing_IsFalse(string loadPath)
        {
            Assert.That(AddressablesStateReader.IsLocalLoadPath(loadPath), Is.False);
        }
    }
}