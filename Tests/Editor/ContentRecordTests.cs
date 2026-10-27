using NUnit.Framework;
using UnityEngine;

namespace Javienne.StartupDiagnostics.Tests
{
    public class ContentRecordTests
    {
        [Test]
        public void GetPath_Platform_IsInTheContentFolderOfTheResults()
        {
            Assert.That(ContentRecord.GetPath("Android"), Is.EqualTo("Library/StartupDiagnostics/Content/Android.json"));
        }

        [Test]
        public void Json_RecordWrittenThenRead_KeepsItsHashes()
        {
            var record = new ContentRecord { Platform = "iOS" };
            record.Entries.Add(new HashedAsset { Guid = "scene", Hash = "1" });
            record.Dependencies.Add(new HashedAsset { Guid = "texture", Hash = "2" });

            var read = JsonUtility.FromJson<ContentRecord>(JsonUtility.ToJson(record));

            Assert.That(read.Platform, Is.EqualTo("iOS"));
            Assert.That(ContentRecord.ToDictionary(read.Entries)["scene"], Is.EqualTo("1"));
            Assert.That(ContentRecord.ToDictionary(read.Dependencies)["texture"], Is.EqualTo("2"));
        }

        [Test]
        public void Read_NoRecord_IsNull()
        {
            Assert.That(ContentRecord.Read("NoSuchPlatform"), Is.Null);
        }
    }
}