# Startup Diagnostics

A Unity package that times the **cold start** of a game, from the start of the process to its first playable screen, on Android, iOS and in Play Mode.

> Work in progress: this README covers the startup timeline only. The measurement window, the Profiler capture, the launch screen and the optimization guide are coming in 1.0.0.

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

## License

MIT, see [LICENSE](LICENSE).