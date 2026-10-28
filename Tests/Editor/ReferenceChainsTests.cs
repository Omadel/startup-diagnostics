using System.Collections.Generic;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class ReferenceChainsTests
    {
        private const string SCENE = "Assets/Scenes/Level.unity";
        private const string PREFAB = "Assets/Dog/Dog.prefab";
        private const string BREED = "Assets/Dog/Breeds/Golden Retriever.asset";
        private const string MATERIAL = "Assets/Dog/Dog.mat";
        private const string TEXTURE = "Assets/Dog/Golden.png";

        private static readonly Dictionary<string, string[]> _dependencies = new()
        {
            { SCENE, new[] { PREFAB, MATERIAL } },
            { PREFAB, new[] { BREED } },
            { BREED, new[] { TEXTURE } },
            { MATERIAL, new[] { PREFAB } },
            { TEXTURE, new string[0] }
        };

        [Test]
        public void FindPath_IndirectAsset_IsTheShortestChain()
        {
            var path = ReferenceChains.FindPath(SCENE, TEXTURE, asset => _dependencies[asset]);

            Assert.That(path, Is.EqualTo(new[] { SCENE, PREFAB, BREED, TEXTURE }));
        }

        [Test]
        public void FindPath_DirectAsset_IsTheSceneAndTheAsset()
        {
            Assert.That(ReferenceChains.FindPath(SCENE, MATERIAL, asset => _dependencies[asset]), Is.EqualTo(new[] { SCENE, MATERIAL }));
        }

        [Test]
        public void FindPath_AssetNotReached_IsNull()
        {
            Assert.That(ReferenceChains.FindPath(TEXTURE, SCENE, asset => _dependencies[asset]), Is.Null);
        }

        [Test]
        public void Format_Path_NamesEachAssetButTheTargetWithWhereItReferencesTheNext()
        {
            var path = new[] { SCENE, PREFAB, BREED, TEXTURE };

            var chain = ReferenceChains.Format(path, (owner, target) => owner == PREFAB ? null : $"{System.IO.Path.GetFileNameWithoutExtension(owner)} to {System.IO.Path.GetFileNameWithoutExtension(target)}");

            Assert.That(chain, Is.EqualTo("Level.unity (Level to Dog) › Dog.prefab › Golden Retriever.asset (Golden Retriever to Golden)"));
        }

        [TestCase("_breeds.Array.data[1]", "_breeds[1]")]
        [TestCase("m_Materials.Array.data[0]", "m_Materials[0]")]
        [TestCase("_settings.Array.data[2]._icons.Array.data[10]", "_settings[2]._icons[10]")]
        [TestCase("_breed", "_breed")]
        public void FormatPropertyPath_ArrayElements_AreWrittenWithBrackets(string propertyPath, string formatted)
        {
            Assert.That(ReferenceChains.FormatPropertyPath(propertyPath), Is.EqualTo(formatted));
        }
    }
}