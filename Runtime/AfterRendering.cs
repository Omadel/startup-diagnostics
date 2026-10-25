using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Runs callbacks once the current frame is rendered, from a system of the player loop added at the end of
    ///     <see cref="PostLateUpdate" />, after the frame is rendered and presented. Not
    ///     <see cref="Awaitable.EndOfFrameAsync" />, which the editor runs as the Game view repaints: later than in players,
    ///     and only while that view is visible.
    /// </summary>
    internal static class AfterRendering
    {
        private static readonly List<Action> _callbacks = new();
        private static readonly List<Action> _running = new();

        /// <summary>
        ///     Runs a callback once the current frame is rendered.
        /// </summary>
        public static void Add(Action callback)
        {
            _callbacks.Add(callback);
        }

        /// <summary>
        ///     Completes once the current frame is rendered.
        /// </summary>
        public static Awaitable WaitAsync()
        {
            var completion = new AwaitableCompletionSource();
            Add(completion.SetResult);
            return completion.Awaitable;
        }

        /// <summary>
        ///     The player loop with the system of this class last in <see cref="PostLateUpdate" />, once, even if it was
        ///     there already: the editor keeps the player loop from one Play Mode to the next without a domain reload.
        /// </summary>
        internal static PlayerLoopSystem WithSystem(PlayerLoopSystem loop, PlayerLoopSystem system)
        {
            if (loop.subSystemList == null) return loop;

            var phases = (PlayerLoopSystem[])loop.subSystemList.Clone();
            for (var i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != typeof(PostLateUpdate)) continue;

                var systems = new List<PlayerLoopSystem>(phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>());
                systems.RemoveAll(other => other.type == system.type);
                systems.Add(system);
                phases[i].subSystemList = systems.ToArray();
            }

            loop.subSystemList = phases;
            return loop;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            _callbacks.Clear();
            var system = new PlayerLoopSystem { type = typeof(AfterRendering), updateDelegate = Run };
            PlayerLoop.SetPlayerLoop(WithSystem(PlayerLoop.GetCurrentPlayerLoop(), system));
        }

        private static void Run()
        {
            if (_callbacks.Count == 0) return;

            // Callbacks added while running are for the next frame.
            _running.AddRange(_callbacks);
            _callbacks.Clear();
            foreach (var callback in _running)
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            _running.Clear();
        }
    }
}