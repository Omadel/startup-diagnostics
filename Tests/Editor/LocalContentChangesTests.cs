using System.Collections.Generic;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class LocalContentChangesTests
    {
        private Dictionary<string, string> _builtEntries;
        private Dictionary<string, string> _builtDependencies;
        private Dictionary<string, string> _currentEntries;
        private Dictionary<string, string> _currentHashes;

        [SetUp]
        public void SetUp()
        {
            _builtEntries = new Dictionary<string, string> { { "scene", "1" }, { "prefab", "2" } };
            _builtDependencies = new Dictionary<string, string> { { "texture", "3" }, { "prefab", "2" } };
            _currentEntries = new Dictionary<string, string>(_builtEntries);
            _currentHashes = new Dictionary<string, string> { { "scene", "1" }, { "prefab", "2" }, { "texture", "3" } };
        }

        [Test]
        public void Find_SameContent_IsEmpty()
        {
            Assert.That(Find(), Is.Empty);
        }

        [Test]
        public void Find_ChangedEntry_IsChanged()
        {
            _currentEntries["scene"] = "9";

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Guid, Is.EqualTo("scene"));
            Assert.That(changes[0].Kind, Is.EqualTo(ContentChangeKind.Changed));
        }

        [Test]
        public void Find_ChangedDependency_IsChanged()
        {
            _currentHashes["texture"] = "9";

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Guid, Is.EqualTo("texture"));
            Assert.That(changes[0].Kind, Is.EqualTo(ContentChangeKind.Changed));
        }

        [Test]
        public void Find_ChangedEntryThatIsAlsoADependency_IsListedOnce()
        {
            _currentEntries["prefab"] = "9";
            _currentHashes["prefab"] = "9";

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Guid, Is.EqualTo("prefab"));
        }

        [Test]
        public void Find_NewEntry_IsAdded()
        {
            _currentEntries["level"] = "5";

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Guid, Is.EqualTo("level"));
            Assert.That(changes[0].Kind, Is.EqualTo(ContentChangeKind.Added));
        }

        [Test]
        public void Find_EntryNoLongerLocal_IsRemovedAndNotChanged()
        {
            _currentEntries.Remove("prefab");
            _currentHashes.Remove("prefab");

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Guid, Is.EqualTo("prefab"));
            Assert.That(changes[0].Kind, Is.EqualTo(ContentChangeKind.Removed));
        }

        [Test]
        public void Find_SeveralChanges_ListsChangedThenAddedThenRemoved()
        {
            _currentEntries.Remove("scene");
            _currentEntries["level"] = "5";
            _currentHashes["texture"] = "9";

            var changes = Find();

            Assert.That(changes, Has.Count.EqualTo(3));
            Assert.That(changes[0].Kind, Is.EqualTo(ContentChangeKind.Changed));
            Assert.That(changes[1].Kind, Is.EqualTo(ContentChangeKind.Added));
            Assert.That(changes[2].Kind, Is.EqualTo(ContentChangeKind.Removed));
        }

        private List<ContentChange> Find()
        {
            return LocalContentChanges.Find(_builtEntries, _builtDependencies, _currentEntries, guid => _currentHashes.TryGetValue(guid, out var hash) ? hash : "0");
        }
    }
}