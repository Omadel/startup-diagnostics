# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-10-28

### Added

- Diagnosis of what weighs on the start, in the editor, in the new **Diagnosis** tab of the window: each check gives a warning, an info or a pass, says what it found and what to do, lists what is in cause, and links to its section of the optimization guide.
  - Settings of the project: the splash screen of Unity, the Preloaded Assets and the Resources folders with the estimated size of their dependencies, the launch screens of the OS, the Unity services at launch, and the devices of the Input System when it is installed.
  - Addressables, when installed: the catalog update at startup, the compression of the local bundles, the JSON catalog, and the local content changed since the last content build with a remote catalog, from a record that the package writes after each content build.
  - Content of the scenes of the start, the first scene of the build and the playable scenes of the settings, each opened as a preview scene: the size of what each one pulls in, with the chain of references to its largest assets, real sizes from the build layout of Addressables 2 when there is one; animations without compression; textures larger than a threshold on Android or iOS; active components with `OnGUI` in the builds; active `UIDocument`s; the first frame of the first scene without the launch screen of the package.
  - With a measure of the start, the checks of its longest step come first, and each check shows the share of its step.
- Diagnosis settings in *Project Settings › Startup Diagnostics*: the playable scenes, the thresholds, and **Check Before Builds**, off by default, which runs the diagnosis before each Android and iOS build and logs its warnings and infos.
- Guide sections for the new checks: the input devices, the Unity services at launch, UI Toolkit in the first scenes, releasing the content with the build, and a first frame like the launch screen.
- A page of the tutorial on the diagnosis, between the results of a measure and what to shorten.
- Edit Mode tests for the checks, the reading of paths and sizes, the chains of references, the changes of the local content, the editor-only `OnGUI`, the times of the steps of a measure and the order of the results.

### Changed

- The window has two tabs, **Measure**, with everything it showed, and **Diagnosis**.

## [1.0.0] - 2026-10-27

### Added

- Startup timeline, `StartupTimeline`, with no required dependency:
  - milestones marked by the package: `scripts_start`, `first_scene_loaded` and `first_frame`;
  - milestones added by the project with `Mark` and `MarkAfterRenderingAsync`;
  - end of the start at the first frame of the first scene of the build, or later with `DeferEnd` then `End`, `EndAfterRenderingAsync` or `EndAtFirstFrameAsync`;
  - origin at the start of the process on Android and iOS, at the start of Play Mode in the editor;
  - one `[Startup]` line in the log, and in `startup.txt` of the temporary cache in players;
  - kept by managed code stripping, even in a project that never calls it.
- `EndAtFirstFrameAsync` for a scene loaded by Addressables, compiled only when Addressables is installed.
- Measure of the cold start on an Android or iOS device, `StartupMeasure`: build, clean install, first launch, then N cold launches, with the median, minimum and maximum of each milestone, written to a JSON file of `Library/StartupDiagnostics` as it goes.
- Window of the measure, in *Window › Analysis › Startup Diagnostics*: its settings, the devices ready, a confirmation before the clean install, a progress bar of the measure under way, step by step, then each milestone of a measure with the time of its step and its change since the previous measure on the same device, flagged above 10%.
- Profiler capture of the start, `MeasureSettings.ProfilerCapture`: a development build records the Profiler from its first scripts for a few seconds, then the measure copies the capture from the device and opens it in the Profiler window. The capture code is compiled only in development builds.
- `IBeforeMeasureBuild`, called just before the build of a measure, out of the player loop.
- `StartupMeasure.RunFromCommandLine`, to measure from the command line with `-executeMethod`.
- Launch screen, off by default, in *Project Settings › Startup Diagnostics*: an icon of the project's choosing at the same place and size from the launch screen of the OS to the end of the start, over the splash background colour of the Player Settings, with a loading ring from the first frame of the game:
  - added to the first scene of each build, and created before the first scene in Play Mode, with a camera of its own; hidden as the end of the start is requested;
  - on Android, in the splash screen of Android 12 and later and in the static splash images of Unity, with the exit animation of the splash screen removed;
  - on iOS, set in the launch screen of the Player Settings before each build.
- Optimization guide, in `Documentation~/optimization-guide.md`: measuring well, then the levers of each step of the start and of the perceived wait, with how to measure each one and the gains measured in Rouflac.
- Tutorial sample for the Tutorial Framework 5.0 or later, imported on demand: the startup timeline, the end of the start, a measure on a device, its results, what to shorten and a Profiler capture, each step checked.
- Edit Mode tests for the line format, the origin, the end of the start, the linking of the timeline, the reading of the `[Startup]` line, the summary of the launches, the adb, bundletool, xcodebuild and devicectl commands, the command line, the checks before a measure, its steps, the comparison of the measures, the request of a Profiler capture, and the launch screen: when it shows, its layout, its images and its Android and iOS build steps.