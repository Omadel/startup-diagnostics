using System.Linq;
using NUnit.Framework;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AfterRenderingTests
    {
        private static readonly PlayerLoopSystem _system = new() { type = typeof(AfterRenderingTests) };

        [Test]
        public void WithSystem_DefaultLoop_AddsItLastInPostLateUpdate()
        {
            var loop = WithSystem(PlayerLoop.GetDefaultPlayerLoop());

            var phase = Phase(loop, typeof(PostLateUpdate));
            Assert.That(phase.subSystemList.Last().type, Is.EqualTo(typeof(AfterRenderingTests)));
            Assert.That(phase.subSystemList.Length, Is.EqualTo(Phase(PlayerLoop.GetDefaultPlayerLoop(), typeof(PostLateUpdate)).subSystemList.Length + 1));
        }

        [Test]
        public void WithSystem_SystemAlreadyThere_KeepsOneLast()
        {
            var once = WithSystem(PlayerLoop.GetDefaultPlayerLoop());
            var phase = Phase(once, typeof(PostLateUpdate));
            phase.subSystemList = new[] { _system }.Concat(phase.subSystemList.Take(phase.subSystemList.Length - 1)).ToArray();

            var loop = WithSystem(Replace(once, phase));

            var systems = Phase(loop, typeof(PostLateUpdate)).subSystemList;
            Assert.That(systems.Count(system => system.type == typeof(AfterRenderingTests)), Is.EqualTo(1));
            Assert.That(systems.Last().type, Is.EqualTo(typeof(AfterRenderingTests)));
        }

        [Test]
        public void WithSystem_OtherPhases_LeavesThemAsTheyAre()
        {
            var loop = WithSystem(PlayerLoop.GetDefaultPlayerLoop());

            var update = Phase(loop, typeof(Update)).subSystemList.Select(system => system.type);
            Assert.That(update, Is.EqualTo(Phase(PlayerLoop.GetDefaultPlayerLoop(), typeof(Update)).subSystemList.Select(system => system.type)));
        }

        [Test]
        public void WithSystem_NoPhase_ReturnsTheLoopAsItIs()
        {
            var loop = WithSystem(new PlayerLoopSystem());

            Assert.That(loop.subSystemList, Is.Null);
        }

        private static PlayerLoopSystem WithSystem(PlayerLoopSystem loop)
        {
            return AfterRendering.WithSystem(loop, _system);
        }

        private static PlayerLoopSystem Phase(PlayerLoopSystem loop, System.Type type)
        {
            return loop.subSystemList.Single(phase => phase.type == type);
        }

        private static PlayerLoopSystem Replace(PlayerLoopSystem loop, PlayerLoopSystem phase)
        {
            var phases = loop.subSystemList.ToArray();
            phases[System.Array.FindIndex(phases, other => other.type == phase.type)] = phase;
            loop.subSystemList = phases;
            return loop;
        }
    }
}