using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How the local Addressables content differs from the last content build, from the hashes of its assets.
    /// </summary>
    internal static class LocalContentChanges
    {
        /// <summary>
        ///     The assets that differ, changed first, then added, then removed, each listed once, ordered by GUID. The
        ///     dictionaries give the hash of each asset by GUID: the entries of the local groups and their dependencies at the
        ///     last content build, and the entries of the local groups now. <paramref name="currentHash" /> gives the hash of
        ///     an asset now.
        /// </summary>
        public static List<ContentChange> Find(IReadOnlyDictionary<string, string> builtEntries, IReadOnlyDictionary<string, string> builtDependencies, IReadOnlyDictionary<string, string> currentEntries, Func<string, string> currentHash)
        {
            var changed = new SortedSet<string>(StringComparer.Ordinal);
            var added = new SortedSet<string>(StringComparer.Ordinal);
            var removed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in builtEntries)
            {
                if (!currentEntries.TryGetValue(entry.Key, out var hash))
                {
                    removed.Add(entry.Key);
                }
                else if (hash != entry.Value)
                {
                    changed.Add(entry.Key);
                }
            }

            foreach (var dependency in builtDependencies)
            {
                if (builtEntries.ContainsKey(dependency.Key) || removed.Contains(dependency.Key)) continue;

                if (currentHash(dependency.Key) != dependency.Value)
                {
                    changed.Add(dependency.Key);
                }
            }

            foreach (var entry in currentEntries)
            {
                if (!builtEntries.ContainsKey(entry.Key))
                {
                    added.Add(entry.Key);
                }
            }

            var changes = new List<ContentChange>();
            Add(changes, changed, ContentChangeKind.Changed);
            Add(changes, added, ContentChangeKind.Added);
            Add(changes, removed, ContentChangeKind.Removed);
            return changes;
        }

        private static void Add(List<ContentChange> changes, IEnumerable<string> guids, ContentChangeKind kind)
        {
            foreach (var guid in guids)
            {
                changes.Add(new ContentChange { Guid = guid, Kind = kind });
            }
        }
    }
}