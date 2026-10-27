namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The steps of the start, as the optimization guide splits them: what a check finds lengthens one of them.
    /// </summary>
    internal enum StartupStep
    {
        /// <summary>
        ///     From the start of the process to <c>scripts_start</c>.
        /// </summary>
        EngineInitialization,

        /// <summary>
        ///     From <c>scripts_start</c> to <c>first_scene_loaded</c>.
        /// </summary>
        FirstScene,

        /// <summary>
        ///     From <c>first_scene_loaded</c> to <c>first_frame</c>.
        /// </summary>
        FirstFrame,

        /// <summary>
        ///     From <c>first_frame</c> to the end of the start: the milestones of the project, such as the initialization of
        ///     Addressables and the loading of the playable scene.
        /// </summary>
        ProjectMilestones,

        /// <summary>
        ///     What shows while the game starts, which does not lengthen it.
        /// </summary>
        PerceivedWait
    }
}