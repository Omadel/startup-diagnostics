using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     What assets take into the build with their dependencies.
    /// </summary>
    internal static class DependencySizes
    {
        /// <summary>
        ///     The size of each root with its dependencies, largest first, and in <paramref name="total" /> the size of all the
        ///     roots together, each dependency counted once. The dependencies of a root include the root itself; the size of
        ///     each path is asked once.
        /// </summary>
        public static List<SizedAsset> Measure(IEnumerable<string> roots, Func<string, IEnumerable<string>> dependenciesOf, Func<string, long> sizeOf, out long total)
        {
            var sizes = new Dictionary<string, long>();
            var counted = new HashSet<string>();
            var measured = new List<SizedAsset>();
            total = 0;
            foreach (var root in roots)
            {
                var size = 0L;
                var dependencies = new HashSet<string>(dependenciesOf(root)) { root };
                foreach (var dependency in dependencies)
                {
                    if (!sizes.TryGetValue(dependency, out var dependencySize))
                    {
                        dependencySize = sizeOf(dependency);
                        sizes.Add(dependency, dependencySize);
                    }

                    size += dependencySize;
                    if (counted.Add(dependency))
                    {
                        total += dependencySize;
                    }
                }

                measured.Add(new SizedAsset { Path = root, Size = size });
            }

            measured.Sort((a, b) => b.Size.CompareTo(a.Size));
            return measured;
        }
    }
}