# Startup Diagnostics

A Unity package that times the **cold start** of a game, from the start of the process to its first playable screen, on Android, iOS and in Play Mode.

It also shows what to shorten: the measure tells which step of the start takes longest, a Profiler capture tells what runs in it, the diagnosis checks in the editor what weighs on the start in the settings of the project and in its first scenes, and the [optimization guide](Documentation~/optimization-guide.md) gives the levers of each step, with the gains measured in a small mobile game.

## Requirements

- Unity 6000.0 or later. The package is verified on Unity 6000.3.
- No other package. With Addressables installed, the timeline can also end at the first frame of a scene that Addressables loads, and the diagnosis checks the settings and the local content of Addressables. With the Input System installed, the diagnosis checks its devices.

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

The measure builds the game, installs it on a device plugged in, launches it once, then launches it N times with the game killed 5 s before each launch, and sums up each milestone over these cold launches: median, minimum and maximum. Relaunched sooner, a device is still warm from the previous launch and starts the game faster than one left alone: 190 ms faster on a Pixel 6 after 2 s.

> **The clean install deletes the data of the game on the device.** Keep the device unlocked with its screen on during the measure: it checks the lock screen before each launch.

On Android, the device needs USB debugging. The measure uses adb, java and bundletool from Unity's Android module, or the SDK and JDK set in *Preferences › External Tools*. An AAB is installed as Google Play would serve it to the device, an APK as is.

On iOS, the measure needs a Mac with Xcode, and a device that trusts the Mac with Developer Mode on, plugged in or on the same network. It builds the Xcode project of Unity for devices with `xcodebuild` (Release, signed by the team of *Player Settings › Signing Team ID*, without archive), and drives the device with `devicectl`. It reads the timeline from `startup.txt`, which the game writes in its temporary cache, because the log of an iOS app is out of reach without a debugger. The Xcode of the Applications folder is used when `DEVELOPER_DIR` is not set.

### From the window

Open **Window › Analysis › Startup Diagnostics**, tab **Measure**.

Under **New measure**, choose the platform, the device (the first available, or one of the devices listed), the number of cold launches, and what to measure:

- **A new build**: builds the game first. Building needs the platform to be the active build target;
- **The last build**: installs the build already at the path;
- **The game on the device**: launches the game as installed, without a clean install nor a first launch.

**Measure** checks that the device is there and unlocked, then asks before the clean install, which deletes the data of the game on the device. The measure goes on in the background. A bar under the toolbar shows how far it is, step by step (the device check, the build, the install, the first launch, then each cold launch), with the time of the step under way and of the whole measure: the build and the install take minutes, during which only these times move. **Cancel**, next to the bar, stops the measure at its next step. The progress bar of the editor shows it too.

The window shows the newest measure, or an older one chosen in the toolbar, with the device, the build and the commit. For each milestone:

| Column | |
| --- | --- |
| First launch | The milestone at the first launch after the clean install, out of the summary. |
| Median, Min, Max | The milestone over the cold launches, in milliseconds since the start of the process. |
| Step | The median time since the milestone before it, and its share of the start: the longest bar is the step to look at first. |
| Previous, Change | The median of the previous measure of the game on the same device, and the change since then. |

A milestone more than 10% slower than in the previous measure is flagged: about twice the spread seen between two series of 10 cold launches of a same build. The **Launches** tab lists the raw values of each launch. The window also follows a measure started from a script or the command line.

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
| `Rebuild` | `true` | Builds the game first, with the settings of the project, as a non-development build. Otherwise measures the build already at `BuildPath`. A new build needs `CleanInstall`. |
| `BuildPath` | `Build/StartupDiagnostics/<platform>/<product name>` | An APK or an AAB on Android, after the setting *Build App Bundle*; the folder of the Xcode project on iOS. |
| `CleanInstall` | `true` | Uninstalls and installs the game, then launches it once, out of the summary. Without it, the game installed on the device is measured. |
| `FirstLaunchWait`, `LaunchWait` | 15 s, 5 s | Time left to the game after the end of its start, before it is killed: the first launch often downloads or caches content. |
| `ProfilerCapture` | `false` | Captures the Profiler of the start instead of timing cold launches, see [Capturing the Profiler of the start](#capturing-the-profiler-of-the-start). |
| `ProfilerSeconds` | 6 s | Length of the Profiler capture, from the first scripts. |

`Start` returns at once. The measure writes its progress and its result to a JSON file of `Library/StartupDiagnostics`, `run.ResultPath`: its `Status` goes from `running` to `succeeded`, `failed` (with an `Error`) or `cancelled`. The file holds the device, its OS and its network, the commit of the project, the build, each launch with its milestones and what the OS reports (`am start -W` on Android), and `ColdSummary`. The network of the device is only read on Android. Script reloads wait for the end of the measure. `run.Cancel()`, or the cancel button of the measure in the progress bar of the editor, stops it.

### Before the build

To prepare the build of a measure, for example to publish remote content, implement `IBeforeMeasureBuild`. Each implementation with a parameterless constructor is called just before the build, out of the player loop so that it may build too, and the build waits for its task. An exception fails the measure, with its message:

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

Leave out `-quit`: the editor quits at the end of the measure, with the exit code 0 if it succeeded and 1 otherwise. Options: `-startupPlatform android|ios` (the active build target by default), `-startupDevice <id>`, `-startupRuns <N>`, `-startupBuild <path>`, `-startupNoBuild` (measure the build already there), `-startupNoInstall` (measure the game on the device, without building nor installing it), `-startupProfilerCapture` (capture the Profiler instead) and `-startupProfilerSeconds <s>`. The measure writes the same JSON file as from the window, and logs its result in a `[Startup Diagnostics]` line.

## Capturing the Profiler of the start

The timeline tells which step of the start is long; the Profiler tells what runs in it. Attaching the Profiler to a device misses the start, and the launch arguments `-profiler-enable -profiler-log-file` stop recording after about 2 s. The measure records it from the game itself instead:

1. it builds the game as a **development build**, to `Build/StartupDiagnostics/<platform>/Development/<product name>` by default;
2. after the clean install and the first launch, it asks the game for a capture, with a file in its persistent data path, then launches it once more;
3. the game records the Profiler from its first scripts for 6 s, to a `.raw` file;
4. the measure copies the capture from the device, next to the JSON file of the measure in `Library/StartupDiagnostics`, and opens it in the Profiler window.

In the window, turn on **Profiler capture** under **New measure**, with the length of the capture, then press **Capture**; from a script, set `ProfilerCapture`. **Open in Profiler** loads the capture of the measure shown again.

A development build starts slower than the game: the launch of the capture stays out of the cold launches and of the comparisons. Compare the share of each step, not its time. The capture code is compiled only in development builds, and records only when the measure asks for it.

## Diagnosing the start

The diagnosis checks in the editor, without a device, what weighs on the start: the settings of the project, and the content of the scenes of the start. Open **Window › Analysis › Startup Diagnostics**, tab **Diagnosis**: it runs the first time the tab is chosen, and again with **Run**.

Each check gives a level:

- **Warning**: the fix is clear and its effect is shown, measured in Rouflac or in a new project, or documented by Unity; or the start breaks;
- **Info**: a cost found without a lever tried, or a list to read;
- **Passed**: nothing to change. The passed checks stay folded.

Each warning and info tells what it found, why it matters to the start and what to do, lists what is in cause (an asset there selects it in the Project window), and links to its section of the [optimization guide](Documentation~/optimization-guide.md) with **Guide**. With a measure of the start, the checks of its longest step come first, and each check shows the share of its step: the newest measure that succeeded with cold launches, of the active build target first.

| Check | Level | What it looks at |
| --- | --- | --- |
| Splash screen of Unity | Warning | Show Splash Screen on in the Player Settings. |
| Preloaded Assets and Resources | Info, Warning above the threshold | The Preloaded Assets of the Player Settings and the content of the Resources folders that goes into the builds, with the estimated size of each one and of its dependencies. |
| Launch screens of the OS | Warning | Without the launch screen of the package: the default launch screen of Unity on iOS, no static splash image on Android. |
| Unity services at launch | Info | Engine Diagnostics, the legacy Unity Analytics initialized on startup, the hardware statistics. |
| Devices of the Input System | Info | No Supported Devices in the settings of the Input System: it creates every device the system reports. |
| Catalog update at startup | Warning | A remote catalog that Addressables checks for while it initializes. |
| Compression of the local bundles | Warning | Groups loaded from the app and compressed with LZMA. |
| Format of the catalog | Info | Enable Json Catalog on in the settings of Addressables 2. |
| Local content and remote catalog | Warning | Local content changed since the last content build, with a remote catalog: a build that builds it again without releasing the content no longer loads its scenes from the second launch on. |
| Content of each scene | Info, Warning above the threshold for the first scene | The size of what the scene pulls in, and its largest assets, each with the chain of references that pulls it in, through components and fields, prefab instances and material properties. |
| Compression of animations | Warning | Models whose clips the Animator controllers of the scenes use, imported without animation compression. |
| Size of textures | Warning | Textures of the scenes larger than the threshold once imported for Android or iOS. |
| OnGUI in the scenes of the start | Warning | Components active when their scene loads with an `OnGUI` method in the builds: the first frame then loads the skin of IMGUI. |
| UI Toolkit in the scenes of the start | Info | `UIDocument`s active when their scene loads. |
| First frame of the first scene | Warning | Without the launch screen of the package: the camera of the first scene clears to another colour than the splash background, to the skybox, or renders no layer. |

The scenes of the start are the first enabled scene of the build, and the **Playable Scenes** of the settings: the scenes of the first playable screen, loaded after it, such as through Addressables. Each one opens as a preview scene, out of the scenes open in the editor, and its assets load to be measured: a run takes about a second in a small game.

The sizes come from the build layout of the last content build of Addressables 2 for the assets of its bundles, when **Debug Build Layout** is on in the preferences of Addressables: their uncompressed size in the bundles. The other sizes are estimated in the editor: textures in their format for the active build target, the rest by its size in memory in the editor.

The check of the local content compares it with a record that the package writes after each content build of Addressables, **Build & Release** included, in `Library/StartupDiagnostics/Content`: it shows once a content build ran with the package installed. The content state of Addressables cannot serve, as it lists only the groups that prevent updates.

### Settings

In **Project Settings › Startup Diagnostics**, under **Diagnosis**:

| Setting | |
| --- | --- |
| **Playable Scenes** | The scenes of the first playable screen, checked with the first scene of the build. Empty, the diagnosis checks the first scene only. |
| **Check Before Builds** | Off by default. Runs the diagnosis before each Android and iOS build: a warning in the console for each warning, a log for each info. |
| **Thresholds** | The size of the Preloaded Assets and Resources together (1 MB), of the content of the first scene (1 MB), the largest size of a texture (1024 px), and the number of assets listed in each list (10). |

### Limits

- An `OnGUI` method counts unless the source of the type that declares it puts it in a block compiled in the editor only, such as `#if UNITY_EDITOR`; a type without source counts as having one.
- The clips counted are those of the Animator controllers that a scene pulls in: clips used through Timeline or a field of a script are not seen.
- The estimated sizes are orders of magnitude; the build layout gives the real ones.

## Shortening the start

The [optimization guide](Documentation~/optimization-guide.md) goes through the steps of the start: engine initialization, the first scene, the first frame, Addressables, the loading of the playable scene, and the perceived wait. For each lever, it tells what the lever does, how to apply it, how to measure its effect, and what it gave in Rouflac, the game this package comes from: its start went from 2.28 to 1.44 s on a Pixel 6 and from 1.16 to 0.73 s on an iPad 11.

## The launch screen

A start that takes long feels shorter when its first images follow one another without a jump: the launch screen of the OS, then the first frame of the game, then its first playable screen. The launch screen of the package shows the same icon, at the same place and the same size, from the launch screen of the OS to the end of the start, with a loading ring under it once the game draws its first frame.

It is **off by default**. Turn it on in **Project Settings › Startup Diagnostics**:

| Setting | |
| --- | --- |
| **Icon** | The image at the centre of the screen, drawn as it is, fitted in a square of 192 points on iOS and 192 dp on Android. A PNG or JPEG file. |
| **Background** | The splash background colour of the Player Settings (**Splash Image › Background Color**), under the icon on every launch screen. |
| **Loading Ring** | A ring that turns under the icon from the second frame of the first scene, with its colour, its diameter and its gap to the icon, in points on iOS and dp on Android. |

What it does with each build:

- **The first scene**: the build adds to it an object with its own camera, drawn over all the others, which shows the icon over the background colour from the first frame, then hides as the end of the start is requested: `End`, `EndAfterRenderingAsync`, or the scene of `EndAtFirstFrameAsync` loaded. The frame that ends the start shows the game. In Play Mode, the object is created before the first scene loads. Nothing to add to a scene.
- **Android**: the splash screen of Android 12 and later shows the icon instead of being empty, and Unity's static splash view, which takes over until the first frame of the game, shows it too, at 192 dp for each screen density. The splash screen of the system goes away at once instead of fading its icon out.
- **iOS**: before each build, the launch screen of the Player Settings is set to the icon at 192 points over the background colour (**Image and background (constant size)**, on iPhone and iPad). A change is logged in the console.

The launch screen shows only while the start goes on after the first frame of the first scene: call `StartupTimeline.DeferEnd` in that scene, as a boot scene does before loading the game (see [Ending the start later](#ending-the-start-later)). Otherwise the first frame ends the start, and the game shows at once.

Android 12 and later crop the icon to the shape of the icons of the device, a circle on Pixel phones: keep what shows inside the circle inscribed in the image. The settings page and the Android build warn when it does not.

## The tutorial

An interactive tutorial walks through the package in the editor: the startup timeline, ending the start on the first playable screen, a measure on a device, its results, the diagnosis, what to shorten, and a Profiler capture. It runs on Unity's Tutorial Framework, which the package does not depend on:

1. Install the **Tutorial Framework** (`com.unity.learn.iet-framework`, 5.0 or later) from the Package Manager.
2. In the Package Manager, select **Startup Diagnostics**, then import the **Tutorial** sample from the **Samples** tab.
3. Open **Tutorials › Show Tutorials**, then **Measure the start of your game**.

Each step that asks for something checks that it is done: the start ended in Play Mode, the measure window open, then a new measure and a new Profiler capture on a device. Following the tutorial to the end needs a device plugged in. The tutorial keeps the open scene and the layout of the editor, and sends no progress to Unity.

The Tutorial Framework has side effects. Remove it once the tutorial is done:

- its runtime assembly goes into the builds, with a method that runs at every start, in the start you measure;
- 6.0.x reopens the Tutorials window at each script reload while **Display Welcome Dialog On Startup** is on, in **Project Settings › Tutorial Framework**: turn it off;
- 5.0.x logs the warning "User not signed in. Please sign in if you want your tutorials progress to be tracked" when the editor is not signed in to a Unity account.

The editor code of the sample compiles only with the framework installed. The tutorial is written with 5.0.x: 6.0.x converts it on import.

## License

MIT, see [LICENSE](LICENSE).