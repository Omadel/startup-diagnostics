# Startup Diagnostics

A Unity package that times the **cold start** of a game, from the start of the process to its first playable screen, on Android, iOS and in Play Mode.

> Work in progress: this README covers the startup timeline and the measure on Android and iOS, from a script or the command line. The window of the measure, the Profiler capture, the launch screen and the optimization guide are coming in 1.0.0.

## Requirements

- Unity 6000.0 or later. The package is verified on Unity 6000.3.
- No other package. With Addressables installed, the timeline can also end at the first frame of a scene that Addressables loads.

## Installation

In the Package Manager, choose **+ › Install package from git URL…** and enter:

```
https://github.com/Omadel/startup-diagnostics.git#v1.0.0
```

Or add this line to the `dependencies` of `Packages/manifest.json`:

```json
"com.javienne.startup-diagnostics": "https://github.com/Omadel/startup-diagnostics.git#v1.0.0"
```

## The startup timeline

Once the package is installed, every start logs one line, in milliseconds since the origin:

```
[Startup] origin=process scripts_start=640 first_scene_loaded=760 first_frame=812
```

| Milestone | When |
| --- | --- |
| `scripts_start` | The first scripts run, once the engine is initialized (`RuntimeInitializeLoadType.SubsystemRegistration`). |
| `first_scene_loaded` | The first scene of the build is loaded and awake (`RuntimeInitializeLoadType.AfterSceneLoad`). |
| `first_frame` | The first frame of that scene is rendered. |

The **origin** is the start of the process on Android and iOS (`origin=process`), and the start of Play Mode in the editor (`origin=play_mode`). On other platforms, it is the start of the engine (`origin=engine`).

The line goes to the log. In players, it is also written to `startup.txt` in `Application.temporaryCachePath`, because the log of an iOS app is out of reach without a debugger.

### Adding milestones

```csharp
using Javienne.StartupDiagnostics;

StartupTimeline.Mark("addressables_ready");
```

Milestones are listed in the order they were recorded. `MarkAfterRenderingAsync` records one once the current frame is rendered.

### Ending the start later

By default, the start ends at the first frame of the first scene of the build. If the first playable screen comes later, for example after a boot scene loads the game scene, defer the end before that frame, then end the start on that screen:

```csharp
using Javienne.StartupDiagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Boot : MonoBehaviour
{
    private void Awake()
    {
        StartupTimeline.DeferEnd();
    }

    private void Start()
    {
        var loading = SceneManager.LoadSceneAsync("Game");
        _ = StartupTimeline.EndAtFirstFrameAsync(loading, "game_loaded", "game_frame");
    }
}
```

- `End` ends the start at once, `EndAfterRenderingAsync` once the current frame is rendered.
- `EndAtFirstFrameAsync` waits for a scene loading, records its first milestone when the scene is loaded, and ends the start once its first frame is rendered. With Addressables installed, it also takes the handle of `Addressables.LoadSceneAsync`.
- Call `DeferEnd` before the first frame is rendered: from `Awake` or `Start` of a component of the first scene, or from a `RuntimeInitializeOnLoadMethod` at `BeforeSceneLoad` or later. The timeline starts over at `SubsystemRegistration`.
- Calling `End` after the start has ended logs a warning, and the milestone is left out. So are milestones marked after the end.

## Measuring the start on a device

The measure builds the game, installs it on a device plugged in, launches it once, then launches it N times with the game killed before each launch, and sums up each milestone over these cold launches: median, minimum and maximum.

> **The clean install deletes the data of the game on the device.** Keep the device unlocked with its screen on during the measure: it checks the lock screen before each launch.

On Android, the device needs USB debugging. The measure uses adb, java and bundletool from Unity's Android module, or the SDK and JDK set in *Preferences › External Tools*. An AAB is installed as Google Play would serve it to the device, an APK as is.

On iOS, the measure needs a Mac with Xcode, and a device that trusts the Mac with Developer Mode on, plugged in or on the same network. It builds the Xcode project of Unity for devices with `xcodebuild` (Release, signed by the team of *Player Settings › Signing Team ID*, without archive), and drives the device with `devicectl`. It reads the timeline from `startup.txt`, which the game writes in its temporary cache, because the log of an iOS app is out of reach without a debugger. The Xcode of the Applications folder is used when `DEVELOPER_DIR` is not set.

### From a script

```csharp
using Javienne.StartupDiagnostics;
using UnityEditor;

var run = StartupMeasure.Start(new MeasureSettings { Platform = BuildTarget.Android, Runs = 10 });
var result = await run.Completion;
```

| Setting | Default | |
| --- | --- | --- |
| `Platform` | `Android` | Building needs it to be the active build target. |
| `Device` | the only one available | The serial of the device on Android; its UDID, identifier or name on iOS. |
| `Runs` | 10 | Cold launches, after the first launch. |
| `Rebuild` | `true` | Builds the game first, with the settings of the project, as a non-development build. Otherwise measures the build already at `BuildPath`. |
| `BuildPath` | `Build/StartupDiagnostics/<platform>/<product name>` | An APK or an AAB on Android, after the setting *Build App Bundle*; the folder of the Xcode project on iOS. |
| `CleanInstall` | `true` | Uninstalls and installs the game, then launches it once, out of the summary. |
| `FirstLaunchWait`, `LaunchWait` | 15 s, 5 s | Time left to the game after the end of its start, before it is killed: the first launch often downloads or caches content. |

`Start` returns at once. The measure writes its progress and its result to a JSON file of `Library/StartupDiagnostics`, `run.ResultPath`: its `Status` goes from `running` to `succeeded`, `failed` (with an `Error`) or `cancelled`. The file holds the device, its OS and its network, the commit of the project, the build, each launch with its milestones and what the OS reports (`am start -W` on Android), and `ColdSummary`. The network of the device is only read on Android. Script reloads wait for the end of the measure. `run.Cancel()`, or the cancel button of the measure in the progress bar of the editor, stops it.

### Before the build

To prepare the build of a measure, for example to publish remote content, implement `IBeforeMeasureBuild`. Each implementation with a parameterless constructor is called just before the build, and the build waits for its task:

```csharp
using System.Threading.Tasks;
using Javienne.StartupDiagnostics;
using UnityEditor;

public class PublishContent : IBeforeMeasureBuild
{
    public async Task OnBeforeMeasureBuildAsync(BuildTarget target)
    {
        await PublishAsync(target);
    }
}
```

### From the command line

With the editor of the project closed:

```
Unity -batchmode -projectPath <project> -buildTarget Android -executeMethod Javienne.StartupDiagnostics.StartupMeasure.RunFromCommandLine
```

Leave out `-quit`: the editor quits at the end of the measure, with the exit code 0 if it succeeded and 1 otherwise. Options: `-startupPlatform android|ios` (the active build target by default), `-startupDevice <id>`, `-startupRuns <N>`, `-startupBuild <path>`, `-startupNoBuild` (measure the build already there) and `-startupNoInstall`.

## License

MIT, see [LICENSE](LICENSE).