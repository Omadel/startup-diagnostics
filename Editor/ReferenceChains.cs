using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     How a scene pulls in an asset: the shortest chain of assets that reference each other, from the scene to the asset.
    /// </summary>
    internal static class ReferenceChains
    {
        private static readonly Regex _arrayElement = new(@"\.Array\.data\[(\d+)\]", RegexOptions.Compiled);

        /// <summary>
        ///     The shortest path of assets from the root to the target, both included, through the direct dependencies of
        ///     each asset, or null when the root does not reach the target.
        /// </summary>
        public static List<string> FindPath(string root, string target, Func<string, IEnumerable<string>> directDependencies)
        {
            var previous = new Dictionary<string, string> { { root, null } };
            var queue = new Queue<string>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var asset = queue.Dequeue();
                if (asset == target) return Unwind(previous, target);

                foreach (var dependency in directDependencies(asset))
                {
                    if (previous.ContainsKey(dependency)) continue;

                    previous.Add(dependency, asset);
                    queue.Enqueue(dependency);
                }
            }

            return null;
        }

        /// <summary>
        ///     A chain for the diagnosis, such as <c>SampleScene.unity (Dog › DogTestController._breeds[1]) › Golden
        ///     Retriever.asset (DogBreed._albedo)</c>: each asset of the path but the target, by file name, with where it
        ///     references the next one when it is known.
        /// </summary>
        public static string Format(IReadOnlyList<string> path, Func<string, string, string> describeReference)
        {
            var chain = new StringBuilder();
            for (var i = 0; i < path.Count - 1; i++)
            {
                if (i > 0)
                {
                    chain.Append(" › ");
                }

                chain.Append(Path.GetFileName(path[i]));
                var where = describeReference(path[i], path[i + 1]);
                if (!string.IsNullOrEmpty(where))
                {
                    chain.Append($" ({where})");
                }
            }

            return chain.ToString();
        }

        /// <summary>
        ///     A serialized property path as written in the diagnosis: <c>_breeds.Array.data[1]</c> as <c>_breeds[1]</c>.
        /// </summary>
        public static string FormatPropertyPath(string propertyPath)
        {
            return _arrayElement.Replace(propertyPath, "[$1]");
        }

        private static List<string> Unwind(Dictionary<string, string> previous, string target)
        {
            var path = new List<string>();
            for (var asset = target; asset != null; asset = previous[asset])
            {
                path.Add(asset);
            }

            path.Reverse();
            return path;
        }
    }
}