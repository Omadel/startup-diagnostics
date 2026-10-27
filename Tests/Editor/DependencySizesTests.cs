using System.Collections.Generic;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class DependencySizesTests
    {
        private static readonly Dictionary<string, long> _sizes = new() { { "a", 10 }, { "b", 20 }, { "shared", 100 }, { "own", 5 } };

        private static readonly Dictionary<string, string[]> _dependencies = new() { { "a", new[] { "a", "shared" } }, { "b", new[] { "shared", "own" } } };

        [Test]
        public void Measure_SharedDependency_CountsInEachRootAndOnceInTheTotal()
        {
            var measured = DependencySizes.Measure(new[] { "a", "b" }, root => _dependencies[root], path => _sizes[path], out var total);

            Assert.That(measured, Has.Count.EqualTo(2));
            Assert.That(measured[0].Path, Is.EqualTo("b"));
            Assert.That(measured[0].Size, Is.EqualTo(125));
            Assert.That(measured[1].Size, Is.EqualTo(110));
            Assert.That(total, Is.EqualTo(135));
        }

        [Test]
        public void Measure_RootMissingFromItsDependencies_CountsTheRoot()
        {
            var measured = DependencySizes.Measure(new[] { "b" }, root => _dependencies[root], path => _sizes[path], out var total);

            Assert.That(measured[0].Size, Is.EqualTo(125));
            Assert.That(total, Is.EqualTo(125));
        }

        [Test]
        public void Measure_SharedDependency_AsksItsSizeOnce()
        {
            var asked = new List<string>();

            DependencySizes.Measure(new[] { "a", "b" }, root => _dependencies[root], path =>
            {
                asked.Add(path);
                return _sizes[path];
            }, out _);

            Assert.That(asked, Is.Unique);
            Assert.That(asked, Has.Count.EqualTo(4));
        }

        [Test]
        public void Measure_NoRoot_IsEmpty()
        {
            var measured = DependencySizes.Measure(new string[0], root => _dependencies[root], path => _sizes[path], out var total);

            Assert.That(measured, Is.Empty);
            Assert.That(total, Is.Zero);
        }
    }
}