# Changelog

All notable changes to this package are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Startup timeline, `StartupTimeline`, with no required dependency:
  - milestones marked by the package: `scripts_start`, `first_scene_loaded` and `first_frame`;
  - milestones added by the project with `Mark` and `MarkAfterRenderingAsync`;
  - end of the start at the first frame of the first scene of the build, or later with `DeferEnd` then `End`, `EndAfterRenderingAsync` or `EndAtFirstFrameAsync`;
  - origin at the start of the process on Android and iOS, at the start of Play Mode in the editor;
  - one `[Startup]` line in the log, and in `startup.txt` of the temporary cache in players.
- `EndAtFirstFrameAsync` for a scene loaded by Addressables, compiled only when Addressables is installed.
- Measure of the cold start on an Android or iOS device, `StartupMeasure`: build, clean install, first launch, then N cold launches, with the median, minimum and maximum of each milestone, written to a JSON file of `Library/StartupDiagnostics` as it goes.
- Window of the measure, in *Window › Analysis › Startup Diagnostics*: its settings, the devices ready, a confirmation before the clean install, then each milestone of a measure with the time of its step and its change since the previous measure on the same device, flagged above 10%.
- `IBeforeMeasureBuild`, called just before the build of a measure.
- `StartupMeasure.RunFromCommandLine`, to measure from the command line with `-executeMethod`.
- Edit Mode tests for the line format, the origin, the end of the start, the reading of the `[Startup]` line, the summary of the launches, the adb, bundletool, xcodebuild and devicectl commands, the command line, the checks before a measure and the comparison of the measures.