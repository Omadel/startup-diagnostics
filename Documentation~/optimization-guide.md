# Startup optimization guide

This guide helps shorten the cold start of a Unity game on Android and iOS, one step of the start at a time, with the startup timeline and the measure of Startup Diagnostics. For each lever, it tells what the lever does, how to apply it, how to measure its effect, and what it gave in Rouflac, the game this package comes from, when it was tried there.

Rouflac is a small mobile game on Unity 6000.3, with URP and Addressables, measured on a Pixel 6 (Android 17) and an iPad 11 (A16). Its numbers are medians of cold launches, in seconds since the start of the process. They give orders of magnitude, not what your game will gain: measure it.

- [How to use this guide](#how-to-use-this-guide)
- [Measuring well](#measuring-well)
- [The steps of the start](#the-steps-of-the-start)
- [Engine initialization](#engine-initialization)
- [The first scene](#the-first-scene)
- [The first frame](#the-first-frame)
- [Addressables](#addressables)
- [Loading the playable scene](#loading-the-playable-scene)
- [Perceived wait](#perceived-wait)

## How to use this guide

1. Measure the start on a device, from a non-development build: see [Measuring the start on a device](../README.md#measuring-the-start-on-a-device).
2. In the window, the **Step** column shows the share of each step in the start: begin with the longest bar, and its chapter below.
3. Capture the Profiler of the start to see what runs in that step: see [Capturing the Profiler of the start](../README.md#capturing-the-profiler-of-the-start). A development build is slower than the game: compare the share of each marker, not its time.
4. Apply one lever, then measure again. The window compares each milestone with the previous measure on the same device, and flags one more than 10% slower.

The package marks three milestones. Mark the steps of your own start with `StartupTimeline.Mark`, for example when your content is ready, and end the start on your first playable screen (see [Ending the start later](../README.md#ending-the-start-later)): each lever then shows in its own step.

## Measuring well

- **Cold launches.** The measure kills the game 5 s before each launch. Relaunched sooner, a device is still warm from the previous launch: 2 s after being killed, Rouflac started 190 ms faster on the Pixel 6.
- **Noise.** Two series of 10 cold launches of the same build differed by up to 5% on the Pixel 6 (1.39 to 1.46 s), and single launches went up to 1.71 s; the iPad 11 gave the same median twice. For a change smaller than that, measure several series, alternating the builds you compare.
- **The first launch** after an install is slower, and stays out of the summary: the game may download or cache content, and Android may still be optimizing the app just after its install. Rouflac takes 1.6 to 3.5 s on the Pixel 6 and 1.7 to 2.0 s on the iPad 11 at its first launch, against 1.4 and 0.7 s cold. Look at it on its own: it is the first impression of the game.
- **Network.** Note the state of the network with the results. Rouflac started in 1.48 s in airplane mode on the Pixel 6, against 1.39 to 1.46 s online.
- **Builds.** Time non-development builds only. A development build is slower to start, by 0.3 s on the Pixel 6 for Rouflac: keep it for the Profiler.

## The steps of the start

| Step | Ends when | What runs |
| --- | --- | --- |
| Start of the process → `scripts_start` | The first scripts run. | The system starts the process, then the engine loads and initializes: native code, graphics device, managed code, preloaded assets. |
| → `first_scene_loaded` | The first scene of the build is loaded and awake. | The splash screen of Unity when it shows, then the loading of the first scene, and the `Awake` and `OnEnable` of its objects. |
| → `first_frame` | The first frame of that scene is rendered. | `Start`, the first `Update`, the first rendering. |
| Your milestones | Your first playable screen. | For example the initialization of Addressables, then the loading of the playable scene and its first frame. |

Rouflac today, with a boot scene that loads the playable scene through Addressables (milestones after `first_frame` marked by the game):

| Milestone | Pixel 6 | iPad 11 |
| --- | --- | --- |
| `scripts_start` | 0.55 s | 0.36 s |
| `first_scene_loaded` | 0.75 s | 0.40 s |
| `first_frame` | 0.97 s | 0.45 s |
| `addressables_ready` | 1.02 s | 0.47 s |
| `scene_loaded` | 1.33 s | 0.69 s |
| `first_scene_frame`, the end of the start | 1.41 s | 0.69 s |

Before the work that this guide sums up, the start ended at 2.28 s on the Pixel 6 and 1.16 s on the iPad 11. The loading of the playable scene took about 60% of it, and gave most of the gain.

## Engine initialization

From the start of the process to `scripts_start`: 0.55 s on the Pixel 6 and 0.36 s on the iPad 11 in Rouflac. No lever was tried on this step there, and the levers of the other steps did not move it.

### Keep the build small

**What it does.** Before the first scripts, the engine loads its native code and the code of the game, converted by IL2CPP, and reads the metadata of the managed code. Less code leaves less to load and read.

**How.** In **Player Settings › Other Settings**, raise **Managed Stripping Level**, then play the whole game: code reached only by reflection may need a `link.xml`. Remove the packages the game does not use.

**Measure.** `scripts_start`, and the size of the build.

### Preloaded assets and Resources

**What it does.** The engine loads the **Preloaded Assets** of the Player Settings before the first scene, and keeps them in memory. It also builds an index of every object in the `Resources` folders while it starts.

**How.** In **Player Settings › Other Settings › Optimization › Preloaded Assets**, keep only what must exist before the first scene: some packages add their settings there. Move assets out of `Resources`, to Addressables or to direct references of the scenes that use them.

**Measure.** `scripts_start` and `first_scene_loaded`.

### Code that runs before the first scene

**What it does.** The `[RuntimeInitializeOnLoadMethod]` of the game and of its packages run inside the start, from `SubsystemRegistration` to `BeforeSceneLoad`, and some systems of Unity initialize there too.

**How.** Capture the Profiler of the start and read its first frames. Move what can wait after the first playable screen.

**Measure.** The step where the work was, in the timeline; its markers in a new capture.

**In Rouflac.** The capture of the Pixel 6 shows 41 ms for the device discovery of the Input System (39 devices, sensors included) and 76 ms for the initialization of UI Toolkit. Not tried.

### Create only the input devices the game uses

**What it does.** Before the first scene, the Input System creates a device for each input that the system reports, sensors included, unless its settings list the devices the game supports.

**How.** In **Project Settings › Input System Package**, add the devices the game uses to **Supported Devices**, such as **Touchscreen** and **Gamepad**: the Input System ignores the others. Then check the input of the game on each platform.

**Measure.** `first_scene_loaded`, and the device discovery of the Input System in a capture of the start.

**In Rouflac.** The capture of the Pixel 6 shows 39 devices and 41 ms for their discovery. Not tried.

### UI Toolkit in the first scenes

**What it does.** The first active `UIDocument` sets up the runtime panel of UI Toolkit while its scene loads.

**How.** If the start does not need UI Toolkit, enable the `UIDocument`s after the first playable screen, and show the start with a camera and a sprite, or with the launch screen of the package.

**Measure.** The step of the scene that holds the `UIDocument`, and the markers of UI Toolkit in a capture of the start.

**In Rouflac.** The capture of the Pixel 6 shows 76 ms for the initialization of UI Toolkit, though no scene of the start has a `UIDocument`: the cause was not found.

### Unity services at launch

**What it does.** Some services of Unity start with the game: Engine Diagnostics, the legacy Unity Analytics when its module is in the project and initializes on startup, and the hardware statistics. They ask the servers of Unity for their settings and write their events on the device.

**How.** Turn off those the game does not need: **Diagnostics Data** in **Project Settings › Services › Diagnostics**; the module **Unity Analytics**, removed in **Package Manager › Built-in**, or its start, with `AnalyticsSettings.initializeOnStartup`; the hardware statistics, with `submitAnalytics: 0` in `ProjectSettings/ProjectSettings.asset`, which the inspector of Unity 6000.3 does not show.

**Measure.** `scripts_start`, online and in airplane mode.

**In Rouflac.** Turned off for privacy: no request to Unity is left at launch. Their cost on the start was not measured.

## The first scene

From `scripts_start` to `first_scene_loaded`.

### Turn off the splash screen of Unity

**What it does.** When it is on, Unity shows its splash screen as long as its animation lasts, before it loads the first scene.

**How.** In **Player Settings › Splash Image**, turn off **Show Splash Screen**. Since Unity 6, every plan can turn it off. Show the [launch screen](#perceived-wait) instead.

**Measure.** `first_scene_loaded`.

**In a new project.** On the Pixel 6, the splash screen of a new project put 2.1 s between `scripts_start` (0.30 s) and `first_scene_loaded` (2.39 s). Rouflac has it off: 0.20 s for this step.

### A small boot scene

**What it does.** The first scene of the build loads before anything of the game shows, with everything it references directly: prefabs, meshes, textures, animations, sounds, ScriptableObjects. A small boot scene loads at once, then loads the playable scene asynchronously while something shows.

**How.** Make the first scene of the build a boot scene: a camera and a component that defers the end of the start and loads the playable scene, as in [Ending the start later](../README.md#ending-the-start-later). Show the launch screen of the package, or the loading screen of the game, while it loads.

**Measure.** `first_scene_loaded`, then your milestones of the playable scene.

**In Rouflac.** `Boot` holds a camera, a bootstrapper and the updater of the remote content. It is loaded and awake 0.05 s after the first scripts on the iPad 11, and 0.20 s after on the Pixel 6.

### Light Awake and OnEnable

**What it does.** The `Awake` and `OnEnable` of the objects of the first scene run before `first_scene_loaded`.

**How.** Keep them short. Start the long work asynchronously, or after the first playable screen, and mark it with `StartupTimeline.Mark` to see its cost.

**Measure.** `first_scene_loaded`.

## The first frame

From `first_scene_loaded` to `first_frame`. The levers below also count for the first frame of every scene after it, such as the playable scene.

### Disable OnGUI until it draws

**What it does.** As soon as a component with an `OnGUI` method is active, even one that draws nothing, the first frame loads the skin of IMGUI: about 0.5 s on a Pixel 6.

**How.** Put each `OnGUI` on a component of its own, disabled until it has something to draw: debug panels, test menus. Or leave `OnGUI` out of release builds.

**Measure.** The step that ends with the first frame of the scene holding the component.

**In Rouflac.** Test panels drawn with IMGUI, hidden at launch, were in the playable scene. Disabling their `OnGUI` until they open took the loading of the playable scene from 0.44 to 0.25 s on the iPad 11, and from 0.87 to 0.45 s on the Pixel 6. The first launch went from 2.29 to 1.70 s and from 3.39 to 2.32 s. The Profiler capture of the start found it, in the first `DrawScreenSpaceUI`.

### The first rendering

**What it does.** The first frame sets up the render pipeline, then loads the shaders and creates the GPU state of each first draw. The more materials and shader variants the first frame draws, the longer it takes.

**How.** Keep the boot scene to a few materials. Strip the shader variants the game does not use, and warm up those of the playable scene while the boot scene shows, with a `ShaderVariantCollection`.

**Measure.** `first_frame`, and the first frame of the playable scene.

**In Rouflac.** The first render pass of URP takes 83 ms in the capture of the Pixel 6. Not tried. On the Pixel 6, the nearly empty `Boot` still takes 0.22 s from its loading to its first frame, against 0.04 s on the iPad 11: not explained yet.

## Addressables

Marked by the game, for example `addressables_ready` once `Addressables.InitializeAsync` is done.

### Do not wait for the network

**What it does.** By default, Addressables checks for a newer remote catalog while it initializes: a request to the server, inside the start, slower on a poor network and offline.

**How.** In the Addressables settings, under **Catalog**, turn on **Only update catalogs manually**. Then check and update the catalogs after the first playable screen, in the background, and download the remote content there. Keep the playable scene in the local content.

**Measure.** `addressables_ready`, and a measure in airplane mode.

**In Rouflac.** No request at launch: the catalog comes from the cache or from the build, and the remote content updates in the background, again and again while offline. In airplane mode, the start took 1.48 s on the Pixel 6, against 1.39 to 1.46 s online.

### Initialize early

**What it does.** `Addressables.InitializeAsync` loads the settings and the catalog. Called by the boot scene, it overlaps with the first frame instead of waiting for the first load.

**How.** Call it in the boot scene, then load the playable scene once it is done.

**Measure.** `addressables_ready`.

**In Rouflac.** Addressables are ready 0.03 s after the first frame of `Boot` on the iPad 11, and 0.05 s after on the Pixel 6. The synchronous part of `InitializeAsync` takes 34 ms in the capture of the Pixel 6.

### Keep the catalog binary

**What it does.** Addressables 2 writes a binary catalog, faster to read than a JSON one, unless **Enable Json Catalog** is on in its settings.

**How.** Leave **Enable Json Catalog** off.

**Measure.** `addressables_ready`.

**In Rouflac.** Binary catalog; not compared with JSON.

### Release the content with the build

**What it does.** With a remote catalog, the game downloads the catalog of the server and uses it at the next launches. When a build carries local bundles that this catalog does not know, because the local content changed and was built again without being released, the game no longer finds them: the playable scene no longer loads, from the second launch on. Not a slow start, but a broken one.

**How.** Release the content with each build whose local content changed: the remote bundles and the catalog of the same content build go to the server before the build ships, such as with **Build & Release** of Cloud Content Delivery. The diagnosis compares the local content with the last content build of the active platform.

**Measure.** Launch the build twice, online.

**In Rouflac.** A build of changed local content, made without Build & Release, loaded its playable scene at the first launch, then no longer from the second one on.

## Loading the playable scene

From the end of the boot scene to the first frame of the playable scene: `scene_loaded` and `first_scene_frame` in Rouflac. It took about 60% of the start there before the work below.

### Find what the scene drags in

**What it does.** Loading a scene loads every asset it references, directly or through its prefabs and ScriptableObjects, decompressed in memory: the size of the scene in memory is what the loading pays for.

**How.** Read the content of its bundle, largest first: the build report of Addressables, in its **Build Reports** window, or the build report of the editor for a scene of the build. A Profiler capture shows the loading itself.

**Measure.** The loading step of the scene, and the size of its bundle.

**In Rouflac.** The 58 animation clips of the dog, imported without compression, made 28.1 MB of the 53 MB of the decompressed bundle of the scene. Integrating the loaded scene took only 33 ms in the capture of the Pixel 6: loading its assets was what cost.

### Compress animations

**How.** In the import settings of the models, tab **Animation**, set **Anim. Compression** to **Optimal**, and check the animations in the game.

### Limit the size of textures per platform

**How.** In the import settings of the textures, override **Max Size** for Android and iOS, for example to 1024. In Rouflac, textures of 2048 px took 2.5 MB each in ASTC.

### Load variants on demand

**What it does.** A direct reference loads its asset with the scene, used or not. An `AssetReference` of Addressables loads it only when asked.

**How.** Reference the variants the start does not need, such as skins, other characters or other levels, with `AssetReference`, and load them when they are chosen.

**In Rouflac.** A test component referenced three breeds of dog: 10 MB of textures loaded with the scene and not used at start.

**Together, in Rouflac.** Compressed animations, textures at 1024 px and breeds loaded on demand took the bundle of the scene from 25.7 to 5.1 MB, and the app from 54.5 to 36.8 MB on iOS and from 62.3 to 44.6 MB on Android. The loading of the playable scene went from 0.69 to 0.44 s on the iPad 11, and from 1.33 to 0.87 s on the Pixel 6. The higher loading priority below came in the same build: the gain of each one is not known.

### Raise the background loading priority

**What it does.** `Application.backgroundLoadingPriority` sets how much time of each frame the asynchronous loading may take on the main thread, where loaded objects are integrated. `High` loads faster, at the cost of the frame rate during the loading.

**How.** Set it to `High` while the playable scene loads, then back to its value:

```csharp
var priority = Application.backgroundLoadingPriority;
Application.backgroundLoadingPriority = ThreadPriority.High;
var loading = Addressables.LoadSceneAsync(scene);
loading.Completed += _ => Application.backgroundLoadingPriority = priority;
_ = StartupTimeline.EndAtFirstFrameAsync(loading, "scene_loaded", "first_scene_frame");
```

**Measure.** The loading step of the scene.

### Compress bundles for where they are

**What it does.** LZ4 bundles are read fast, chunk by chunk. LZMA bundles are smaller to download, but must be decompressed whole: Unity recompresses them to LZ4 in its cache.

**How.** LZ4 for the bundles in the app, LZMA for the remote ones, in the **Compression** of each Addressables group.

**In Rouflac.** Local group in LZ4, remote group in LZMA; not compared.

## Perceived wait

When the start cannot get shorter, it can feel shorter: one image, the same from the launch screen of the system to the first playable screen, without a jump, and a sign that the game is loading.

### The launch screen of the package

**What it does.** It shows one icon, at the same place and size, on the launch screen of the system, in the first frames of the game and until the start ends, with a loading ring.

**How.** Turn it on in **Project Settings › Startup Diagnostics**, and defer the end of the start in the boot scene: see [The launch screen](../README.md#the-launch-screen).

**Measure.** The timeline, to check that it costs nothing; a screen recording, to check the images: `adb shell screenrecord` on Android, read frame by frame.

**In Rouflac.** + 0.5 MB on iOS and + 0.8 MB on Android. Nothing measurable on the iPad 11, 0 to 60 ms on the Pixel 6, within the noise. A screen recording of the Pixel 6 shows the same disc of 505 px, at the same place, from the splash screen of the system to the first scene, without a jump.

What it handles, for a game that would do it itself:

- **iOS** shows the launch storyboard until the first frame of Unity: **Image and background (constant size)**, the icon at 192 points, in the Player Settings.
- **Android 12 and later** show a splash screen of the system. The theme that Unity 6000.3 generates gives it a transparent icon: the splash screen is empty. Without an icon background colour in the theme, the system draws an adaptive icon and shows its middle 192 dp, cropped to the shape of the icons of the device.
- **Android** then shows the static splash view of Unity, from the first frame of the app (0.13 s on the Pixel 6) to the first frame of Unity. It draws the image `unity_static_splash` at its size in pixels, whatever the density of the screen: one image per density.
- The splash screen of the system fades its icon out, then fades the app in: the icon blinks. Removing the splash screen at once in its exit listener (`SplashScreenView.remove`) keeps the image still.
- **The first scene** shows the same icon at the same size: 192 points times the scale of the view on iOS, 192 dp times the density on Android. A camera whose culling mask renders no layer shows only its background colour.

### A first frame like the launch screen

**What it does.** Without the launch screen of the package, the first frame of the first scene follows the launch screen of the system directly. A camera that clears to another colour or to the skybox, or that renders no layer and so shows its background without the icon, makes the screen jump.

**How.** Clear the camera of the first scene to the splash background colour of the Player Settings, and draw the icon of the launch screen at the same place and size. Or turn on the launch screen of the package, which does both.

**Measure.** A screen recording, read frame by frame.

**In Rouflac.** A camera of `Boot` whose culling mask rendered no layer showed only its background colour, without the icon.

### An indicator from the second frame

**What it does.** A loading ring tells that the game is starting. Shown from the second frame of the game only, it keeps the first frame identical to the launch screen of the system.

**How.** The launch screen of the package fades its ring in over 0.3 s from the second frame. For a loading screen of your own, start its animation after the first frame.

### No splash screen of Unity

The splash screen of Unity adds its animation to the wait, before the first scene: see [Turn off the splash screen of Unity](#turn-off-the-splash-screen-of-unity).