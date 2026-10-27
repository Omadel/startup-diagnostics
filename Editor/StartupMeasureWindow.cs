using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     Window of the startup measure, in Window › Analysis: the settings of a new measure and its start, then a measure,
    ///     the newest by default, with each milestone over its cold launches, the time each step takes, and the change since
    ///     the previous measure of the game on the same device.
    /// </summary>
    internal sealed class StartupMeasureWindow : EditorWindow
    {
        private const string TITLE = "Startup Diagnostics";
        private const string FIRST_AVAILABLE = "First available";
        private const long POLL_MILLISECONDS = 500;
        private const float SHARE_WIDTH = 48;
        private const int SOURCE_NEW_BUILD = 0;
        private const int SOURCE_LAST_BUILD = 1;
        private const int SOURCE_INSTALLED_GAME = 2;

        private static readonly List<string> _platformChoices = new() { "Android", "iOS" };
        private static readonly List<string> _sourceChoices = new() { "A new build", "The last build", "The game on the device" };
        private static readonly Color _slowerColor = new(0.9f, 0.32f, 0.29f);
        private static readonly Color _fasterColor = new(0.36f, 0.72f, 0.36f);
        private static readonly Color _shareColor = new(0.27f, 0.52f, 0.93f);
        private static readonly Color _shareTrackColor = new(0.5f, 0.5f, 0.5f, 0.15f);

        // Kept across domain reloads and editor restarts, for this window.
        [SerializeField] private MeasureSettings _settings;
        [SerializeField] private bool _hasSettings;

        // Empty follows the newest measure.
        [SerializeField] private string _shownPath;

        private readonly List<MilestoneRow> _rows = new();
        private readonly List<LaunchRow> _launches = new();
        private List<MeasureFile> _files = new();
        private MeasureFile _shown;
        private MeasureFile _previous;
        private List<ListedDevice> _devices = new();
        private List<string> _deviceIds = new();
        private string _deviceMessage;
        private CancellationTokenSource _listing;
        private CancellationTokenSource _checking;
        private bool _wasRunning;
        private string _launchColumns;

        private ToolbarButton _measureMenuButton;
        private ToolbarButton _revealButton;
        private VisualElement _progressRow;
        private ProgressBar _progressBar;
        private Button _cancelButton;
        private VisualElement _settingsPanel;
        private DropdownField _platformField;
        private DropdownField _deviceField;
        private Button _devicesButton;
        private HelpBox _deviceBox;
        private IntegerField _runsField;
        private DropdownField _sourceField;
        private VisualElement _buildRow;
        private TextField _buildField;
        private Toggle _captureField;
        private FloatField _captureSecondsField;
        private FloatField _firstLaunchWaitField;
        private FloatField _launchWaitField;
        private HelpBox _issueBox;
        private Button _measureButton;
        private VisualElement _result;
        private HelpBox _emptyBox;
        private Label _details;
        private HelpBox _statusBox;
        private HelpBox _comparisonBox;
        private VisualElement _captureRow;
        private HelpBox _captureBox;
        private Button _openCaptureButton;
        private MultiColumnListView _milestoneList;
        private Tab _launchesTab;
        private MultiColumnListView _launchList;

        /// <summary>
        ///     Column of a list: title, tooltip, width, the text of a cell for an item, its colour, and the share of a bar
        ///     drawn before the text.
        /// </summary>
        private sealed class ColumnSpec<T>
        {
            public string Title;
            public string Tooltip;
            public float Width;
            public bool Stretch;
            public bool AlignRight;
            public Func<T, string> Text;
            public Func<T, Color?> Color;
            public Func<T, float> Share;
        }

        /// <summary>
        ///     A launch of the shown measure: the first one after the clean install, or a cold launch.
        /// </summary>
        private sealed class LaunchRow
        {
            public string Label;
            public Launch Launch;
        }

        [MenuItem("Window/Analysis/Startup Diagnostics")]
        internal static void Open()
        {
            var window = GetWindow<StartupMeasureWindow>();
            window.minSize = new Vector2(640, 480);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(TITLE);
            if (!_hasSettings)
            {
                var active = EditorUserBuildSettings.activeBuildTarget;
                _settings = new MeasureSettings { Platform = active == BuildTarget.iOS ? BuildTarget.iOS : BuildTarget.Android };
                _hasSettings = true;
            }
        }

        private void OnDisable()
        {
            _listing?.Cancel();
            _checking?.Cancel();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new Toolbar();
            _measureMenuButton = new ToolbarButton(ShowMeasureMenu) { tooltip = "Measure to show. The newest one follows new measures." };
            toolbar.Add(_measureMenuButton);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            _revealButton = new ToolbarButton(RevealShown) { text = Application.platform == RuntimePlatform.OSXEditor ? "Reveal in Finder" : "Show in Explorer", tooltip = "The JSON file of the measure, with the raw values of each launch." };
            toolbar.Add(_revealButton);
            toolbar.Add(new ToolbarButton(Refresh) { text = "Refresh" });
            root.Add(toolbar);

            _progressRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginLeft = 4, marginRight = 4, marginTop = 4 } };
            _progressBar = new ProgressBar { style = { flexGrow = 1, flexShrink = 1 } };
            _cancelButton = new Button(CancelMeasure) { text = "Cancel", tooltip = "Stops the measure at its next step; the game may be left running on the device." };
            _progressRow.Add(_progressBar);
            _progressRow.Add(_cancelButton);
            root.Add(_progressRow);

            root.Add(CreateSettings());
            root.Add(CreateResult());

            UpdateSettingsFields();
            Refresh();
            RefreshDevices();
            root.schedule.Execute(Poll).Every(POLL_MILLISECONDS);
        }

        private VisualElement CreateSettings()
        {
            var foldout = new Foldout { text = "New measure", value = true, style = { marginLeft = 4, marginRight = 4, marginTop = 2 } };
            _settingsPanel = new VisualElement();
            foldout.Add(_settingsPanel);

            _platformField = new DropdownField("Platform", _platformChoices, 0) { tooltip = "Building for the measure needs the platform to be the active build target." };
            _platformField.RegisterValueChangedCallback(change =>
            {
                _settings.Platform = _platformField.index == 1 ? BuildTarget.iOS : BuildTarget.Android;
                _settings.Device = null;
                _settings.BuildPath = null;
                UpdateSettingsFields();
                RefreshDevices();
            });
            _settingsPanel.Add(_platformField);

            var deviceRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _deviceField = new DropdownField("Device") { tooltip = "The device to measure on. First available takes the only device ready, and asks to choose when there are several.", style = { flexGrow = 1, flexShrink = 1 } };
            _deviceField.RegisterValueChangedCallback(change =>
            {
                var index = _deviceField.index;
                _settings.Device = index > 0 && index < _deviceIds.Count ? _deviceIds[index] : null;
            });
            _devicesButton = new Button(RefreshDevices) { text = "Refresh", tooltip = "Lists the devices ready again." };
            deviceRow.Add(_deviceField);
            deviceRow.Add(_devicesButton);
            _settingsPanel.Add(deviceRow);
            _deviceBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _settingsPanel.Add(_deviceBox);

            _runsField = new IntegerField("Cold launches") { isDelayed = true, tooltip = "Launches with the game killed before each one, after the first launch. Their median, minimum and maximum make the result." };
            _runsField.RegisterValueChangedCallback(change => _settings.Runs = change.newValue);
            _settingsPanel.Add(_runsField);

            _sourceField = new DropdownField("Measure", _sourceChoices, 0) { tooltip = "A new build: builds the game with the settings of the project, as a non-development build, or a development build for a Profiler capture, then installs it.\nThe last build: installs the build already there.\nThe game on the device: launches the game as installed, without a clean install nor a first launch." };
            _sourceField.RegisterValueChangedCallback(change =>
            {
                _settings.Rebuild = _sourceField.index == SOURCE_NEW_BUILD;
                _settings.CleanInstall = _sourceField.index != SOURCE_INSTALLED_GAME;
                UpdateSettingsFields();
            });
            _settingsPanel.Add(_sourceField);

            _buildRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _buildField = new TextField("Build") { isDelayed = true, style = { flexGrow = 1, flexShrink = 1 } };
            _buildField.textEdition.hidePlaceholderOnFocus = true;
            _buildField.RegisterValueChangedCallback(change => _settings.BuildPath = string.IsNullOrWhiteSpace(change.newValue) ? null : change.newValue.Trim());
            _buildRow.Add(_buildField);
            _buildRow.Add(new Button(BrowseBuild) { text = "…", tooltip = "Chooses the build." });
            _settingsPanel.Add(_buildRow);

            _captureField = new Toggle("Profiler capture") { tooltip = "Captures the Profiler instead of timing cold launches: a development build, launched once more after the first launch, records the Profiler from its first scripts. The capture opens in the Profiler window." };
            _captureField.RegisterValueChangedCallback(change =>
            {
                _settings.ProfilerCapture = change.newValue;
                UpdateSettingsFields();
            });
            _settingsPanel.Add(_captureField);
            _captureSecondsField = new FloatField("Capture (s)") { isDelayed = true, tooltip = "Seconds of the capture, from the first scripts." };
            _captureSecondsField.RegisterValueChangedCallback(change => _settings.ProfilerSeconds = change.newValue);
            _settingsPanel.Add(_captureSecondsField);

            var waits = new Foldout { text = "Waits", value = false };
            _firstLaunchWaitField = new FloatField("After the first launch (s)") { isDelayed = true, tooltip = "Time left to the game after the end of its start at the first launch, before it is killed: the first launch often downloads or caches content that the next launches use." };
            _firstLaunchWaitField.RegisterValueChangedCallback(change => _settings.FirstLaunchWait = change.newValue);
            _launchWaitField = new FloatField("After each cold launch (s)") { isDelayed = true, tooltip = "Time left to the game after the end of its start at each cold launch, before it is killed." };
            _launchWaitField.RegisterValueChangedCallback(change => _settings.LaunchWait = change.newValue);
            waits.Add(_firstLaunchWaitField);
            waits.Add(_launchWaitField);
            _settingsPanel.Add(waits);

            _issueBox = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            foldout.Add(_issueBox);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 4 } };
            _measureButton = new Button(StartMeasure) { text = "Measure", style = { minWidth = 100 } };
            buttons.Add(_measureButton);
            foldout.Add(buttons);
            return foldout;
        }

        private VisualElement CreateResult()
        {
            var container = new VisualElement { style = { flexGrow = 1, marginLeft = 4, marginRight = 4, marginTop = 6 } };
            _emptyBox = new HelpBox("No measure yet in " + StartupMeasure.RESULTS_FOLDER + ": start one above.", HelpBoxMessageType.Info);
            container.Add(_emptyBox);

            _result = new VisualElement { style = { flexGrow = 1 } };
            _details = new Label { style = { whiteSpace = WhiteSpace.Normal, marginLeft = 2, marginBottom = 4 } };
            _result.Add(_details);
            _statusBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _result.Add(_statusBox);
            _comparisonBox = new HelpBox(string.Empty, HelpBoxMessageType.Info);
            _result.Add(_comparisonBox);
            _captureRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _captureBox = new HelpBox(string.Empty, HelpBoxMessageType.Info) { style = { flexGrow = 1, flexShrink = 1 } };
            _openCaptureButton = new Button(OpenShownCapture) { text = "Open in Profiler", tooltip = "Loads the capture in the Profiler window, in place of the frames it shows." };
            _captureRow.Add(_captureBox);
            _captureRow.Add(_openCaptureButton);
            _result.Add(_captureRow);

            var tabs = new TabView { style = { flexGrow = 1, marginTop = 4 } };
            tabs.contentContainer.style.flexGrow = 1;
            var milestonesTab = new Tab("Milestones") { style = { flexGrow = 1 } };
            milestonesTab.contentContainer.style.flexGrow = 1;
            _milestoneList = CreateList(_rows,
                new ColumnSpec<MilestoneRow> { Title = "Milestone", Width = 150, Stretch = true, Text = row => row.Name, Tooltip = "The milestones of the timeline, in milliseconds since the start of the process, then the first frame of the app according to the OS." },
                new ColumnSpec<MilestoneRow> { Title = "First launch", Width = 80, AlignRight = true, Text = row => row.FirstLaunch < 0 ? string.Empty : row.FirstLaunch.ToString(CultureInfo.InvariantCulture), Tooltip = "The first launch after the clean install, out of the summary: it often downloads or caches content." },
                new ColumnSpec<MilestoneRow> { Title = "Median", Width = 70, AlignRight = true, Text = row => row.Cold == null ? string.Empty : MeasureReport.FormatMilliseconds(row.Cold.Median), Tooltip = "The median over the cold launches." },
                new ColumnSpec<MilestoneRow> { Title = "Min", Width = 60, AlignRight = true, Text = row => row.Cold == null ? string.Empty : row.Cold.Min.ToString(CultureInfo.InvariantCulture) },
                new ColumnSpec<MilestoneRow> { Title = "Max", Width = 60, AlignRight = true, Text = row => row.Cold == null ? string.Empty : row.Cold.Max.ToString(CultureInfo.InvariantCulture) },
                new ColumnSpec<MilestoneRow> { Title = "Step", Width = 110, AlignRight = true, Text = row => MeasureReport.FormatMilliseconds(row.Step), Share = row => MeasureReport.GetShare(row, _rows), Tooltip = "The median time since the milestone before it in each cold launch, and its share of the whole start." },
                new ColumnSpec<MilestoneRow> { Title = "Previous", Width = 70, AlignRight = true, Text = row => row.Previous == null ? string.Empty : MeasureReport.FormatMilliseconds(row.Previous.Median), Tooltip = "The median of the previous measure of the game on this device." },
                new ColumnSpec<MilestoneRow> { Title = "Change", Width = 65, AlignRight = true, Text = row => MeasureReport.FormatChange(row.Change), Color = GetChangeColor, Tooltip = "The change of the median since the previous measure, flagged above " + FormatThreshold() + "." });
            milestonesTab.Add(_milestoneList);
            tabs.Add(milestonesTab);
            _launchesTab = new Tab("Launches") { style = { flexGrow = 1 } };
            _launchesTab.contentContainer.style.flexGrow = 1;
            tabs.Add(_launchesTab);
            _result.Add(tabs);
            container.Add(_result);
            return container;
        }

        private static MultiColumnListView CreateList<T>(List<T> items, params ColumnSpec<T>[] specs)
        {
            var list = new MultiColumnListView { itemsSource = items, selectionType = SelectionType.Single, showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly, fixedItemHeight = 20, style = { flexGrow = 1 } };
            foreach (var spec in specs)
            {
                list.columns.Add(new Column
                {
                    title = spec.Title,
                    width = spec.Width,
                    stretchable = spec.Stretch,
                    makeHeader = () => CreateHeader(spec.Title, spec.Tooltip, spec.AlignRight),
                    makeCell = () => spec.Share == null ? CreateLabel(spec.AlignRight) : CreateShareCell(),
                    bindCell = (element, index) => BindCell(element, spec, (T)((IList)list.itemsSource)[index])
                });
            }

            return list;
        }

        private static Label CreateHeader(string title, string tooltip, bool alignRight)
        {
            return new Label(title) { tooltip = tooltip, style = { flexGrow = 1, unityTextAlign = alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 4, paddingRight = 4, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
        }

        private static Label CreateLabel(bool alignRight)
        {
            return new Label { style = { flexGrow = 1, unityTextAlign = alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap, paddingLeft = 4, paddingRight = 4 } };
        }

        private static VisualElement CreateShareCell()
        {
            var cell = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 4 } };
            var track = new VisualElement { style = { width = SHARE_WIDTH, height = 8, flexShrink = 0, backgroundColor = _shareTrackColor } };
            track.Add(new VisualElement { name = "share", style = { height = Length.Percent(100), backgroundColor = _shareColor } });
            cell.Add(track);
            cell.Add(CreateLabel(true));
            return cell;
        }

        private static void BindCell<T>(VisualElement element, ColumnSpec<T> spec, T item)
        {
            var label = element as Label ?? element.Q<Label>();
            label.text = spec.Text(item) ?? string.Empty;
            var color = spec.Color?.Invoke(item);
            label.style.color = color.HasValue ? new StyleColor(color.Value) : new StyleColor(StyleKeyword.Null);
            if (spec.Share == null) return;

            var share = spec.Share(item);
            element.Q("share").style.width = Length.Percent(share * 100);
            element.tooltip = share > 0 ? $"{Mathf.RoundToInt(share * 100)}% of the start" : string.Empty;
        }

        private static Color? GetChangeColor(MilestoneRow row)
        {
            if (row.IsSlower) return _slowerColor;
            if (row.Change < -MeasureReport.SLOWER_THRESHOLD) return _fasterColor;

            return null;
        }

        private static string FormatThreshold()
        {
            return (MeasureReport.SLOWER_THRESHOLD * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        ///     Follows a running measure, and the start and the end of a measure started elsewhere, through the API or the
        ///     MCP.
        /// </summary>
        private void Poll()
        {
            var running = StartupMeasure.IsRunning;
            if (running != _wasRunning)
            {
                _wasRunning = running;
                if (running)
                {
                    _shownPath = null;
                }

                Refresh();
            }
            else if (running && _shown != null && _shown.Result == StartupMeasure.Current.Result)
            {
                UpdateResult();
            }

            UpdateSettingsState();
        }

        /// <summary>
        ///     Reads the measures again, with the running one as it goes, and shows the chosen one.
        /// </summary>
        private void Refresh()
        {
            if (_result == null) return;

            _files = MeasureHistory.Load(StartupMeasure.RESULTS_FOLDER);
            var current = StartupMeasure.Current;
            if (current != null)
            {
                var path = current.ResultPath.Replace('\\', '/');
                var file = _files.FirstOrDefault(other => other.Path == path);
                if (file != null)
                {
                    file.Result = current.Result;
                }
                else if (!current.IsDone)
                {
                    _files = MeasureHistory.SortNewestFirst(_files.Append(new MeasureFile(path, current.Result)));
                }
            }

            _shown = string.IsNullOrEmpty(_shownPath) ? _files.FirstOrDefault() : _files.FirstOrDefault(file => file.Path == _shownPath) ?? _files.FirstOrDefault();
            UpdateResult();
            UpdateSettingsState();
        }

        private void UpdateResult()
        {
            _measureMenuButton.text = _shown == null ? "No measure" : GetMeasureLabel(_shown, string.IsNullOrEmpty(_shownPath));
            _revealButton.SetEnabled(_shown != null);
            _emptyBox.style.display = _shown == null ? DisplayStyle.Flex : DisplayStyle.None;
            _result.style.display = _shown == null ? DisplayStyle.None : DisplayStyle.Flex;
            _rows.Clear();
            _launches.Clear();
            if (_shown == null)
            {
                _milestoneList.Rebuild();
                _launchList?.Rebuild();
                return;
            }

            var result = _shown.Result;
            _previous = MeasureHistory.FindPrevious(_files, _shown);
            _rows.AddRange(MeasureReport.GetRows(result, _previous?.Result));
            _milestoneList.Rebuild();
            _details.text = GetDetails(result);
            UpdateStatus(result);

            var comparison = MeasureReport.GetComparison(_rows, _previous == null ? null : GetPreviousLabel(_previous.Result));
            var hasCold = result.ColdSummary.Count > 0;
            _comparisonBox.text = comparison.Message;
            _comparisonBox.messageType = comparison.IsWarning ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _comparisonBox.style.display = hasCold ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateCapture(result);

            if (result.HasFirstLaunch)
            {
                _launches.Add(new LaunchRow { Label = "First", Launch = result.FirstLaunch });
            }

            for (var i = 0; i < result.ColdLaunches.Count; i++)
            {
                _launches.Add(new LaunchRow { Label = (i + 1).ToString(CultureInfo.InvariantCulture), Launch = result.ColdLaunches[i] });
            }

            if (result.CaptureLaunch != null && result.CaptureLaunch.Milestones.Count > 0)
            {
                _launches.Add(new LaunchRow { Label = "Capture", Launch = result.CaptureLaunch });
            }

            UpdateLaunchList();
        }

        private void UpdateCapture(MeasureResult result)
        {
            var hasCapture = result.ProfilerCapture && !string.IsNullOrEmpty(result.CapturePath);
            _captureRow.style.display = hasCapture ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasCapture) return;

            var exists = File.Exists(result.CapturePath);
            _captureBox.text = exists ? $"Profiler capture of the first {FormatSeconds(result.ProfilerSeconds)} of the start, in a development build, slower to start than the game: {result.CapturePath}" : $"The Profiler capture is no longer at {result.CapturePath}.";
            _openCaptureButton.SetEnabled(exists);
        }

        private void OpenShownCapture()
        {
            if (_shown == null || StartupMeasure.OpenProfilerCapture(_shown.Result.CapturePath)) return;

            EditorUtility.DisplayDialog(TITLE, $"Unable to open the Profiler capture {_shown.Result.CapturePath}.", "OK");
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds.ToString("0.#", CultureInfo.InvariantCulture) + " s";
        }

        private void UpdateStatus(MeasureResult result)
        {
            var current = StartupMeasure.Current;
            var isCurrent = current != null && current.Result == result && !current.IsDone;
            switch (result.Status)
            {
                case MeasureStatus.RUNNING:
                    _statusBox.text = $"Not finished, at: {result.Step}. The measure runs in another editor, or stopped before its end.";
                    _statusBox.messageType = HelpBoxMessageType.Warning;
                    break;
                case MeasureStatus.FAILED:
                    _statusBox.text = $"Failed at {result.Step}: {result.Error}";
                    _statusBox.messageType = HelpBoxMessageType.Error;
                    break;
                case MeasureStatus.CANCELLED:
                    _statusBox.text = $"Cancelled at {result.Step}.";
                    _statusBox.messageType = HelpBoxMessageType.Warning;
                    break;
            }

            // The progress bar shows the step of the running measure.
            _statusBox.style.display = result.Status == MeasureStatus.SUCCEEDED || isCurrent ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        ///     Rebuilds the list of launches when its milestones change, with a column per milestone.
        /// </summary>
        private void UpdateLaunchList()
        {
            var names = new List<string>();
            foreach (var row in _launches)
            {
                foreach (var milestone in row.Launch.Milestones)
                {
                    if (!names.Contains(milestone.Name))
                    {
                        names.Add(milestone.Name);
                    }
                }
            }

            var hasOsFirstFrame = _launches.Any(row => row.Launch.OsFirstFrame >= 0);
            var hasOsLaunchState = _launches.Any(row => !string.IsNullOrEmpty(row.Launch.OsLaunchState));
            var columns = string.Join(" ", names) + (hasOsFirstFrame ? " os" : string.Empty) + (hasOsLaunchState ? " state" : string.Empty);
            if (_launchList != null && columns == _launchColumns)
            {
                _launchList.Rebuild();
                return;
            }

            var specs = new List<ColumnSpec<LaunchRow>> { new ColumnSpec<LaunchRow> { Title = "Launch", Width = 60, Text = row => row.Label, Tooltip = "The first launch after the clean install, then the cold launches in their order." } };
            foreach (var name in names)
            {
                specs.Add(new ColumnSpec<LaunchRow> { Title = name, Width = 110, AlignRight = true, Text = row => row.Launch.TryGetMilestone(name, out var milliseconds) ? milliseconds.ToString(CultureInfo.InvariantCulture) : string.Empty, Tooltip = $"{name}, in milliseconds since the start of the process." });
            }

            if (hasOsFirstFrame)
            {
                specs.Add(new ColumnSpec<LaunchRow> { Title = MilestoneSummary.OS_FIRST_FRAME, Width = 110, AlignRight = true, Text = row => row.Launch.OsFirstFrame < 0 ? string.Empty : row.Launch.OsFirstFrame.ToString(CultureInfo.InvariantCulture), Tooltip = "The first frame of the app according to the OS (am start -W on Android), in milliseconds." });
            }

            if (hasOsLaunchState)
            {
                specs.Add(new ColumnSpec<LaunchRow> { Title = "OS launch", Width = 80, Text = row => row.Launch.OsLaunchState, Tooltip = "The kind of launch according to the OS: COLD, WARM or HOT." });
            }

            _launchList?.RemoveFromHierarchy();
            _launchList = CreateList(_launches, specs.ToArray());
            _launchesTab.Add(_launchList);
            _launchColumns = columns;
        }

        private static string GetDetails(MeasureResult result)
        {
            var device = string.Join(", ", new[] { result.DeviceModel, result.OsVersion, result.Network == MeasureResult.NETWORK_ONLINE || result.Network == MeasureResult.NETWORK_OFFLINE ? result.Network : null }.Where(part => !string.IsNullOrEmpty(part)));
            var build = result.Rebuilt ? "New build" : result.CleanInstall ? "Last build" : "Game on the device";
            if (!string.IsNullOrEmpty(result.BuildPath))
            {
                build += " " + result.BuildPath;
            }

            var launches = (result.ProfilerCapture ? $"Profiler capture of {FormatSeconds(result.ProfilerSeconds)}" : $"{result.ColdLaunches.Count} of {result.Runs} cold launches") + (result.CleanInstall ? " after a clean install" : string.Empty);
            var duration = MeasureHistory.GetDuration(result);
            var started = "Started " + MeasureHistory.FormatStartedAt(result, TimeZoneInfo.Local) + (duration.HasValue ? ", took " + MeasureReport.FormatDuration(duration.Value) : string.Empty);
            var lines = new[] { string.IsNullOrEmpty(device) ? result.Platform : $"{result.Platform} · {device}", $"{result.ApplicationIdentifier} · {build}" + (string.IsNullOrEmpty(result.Commit) ? string.Empty : $" · commit {result.Commit}") + $" · Unity {result.UnityVersion}", $"{launches} · {started}" };
            return string.Join("\n", lines);
        }

        private static string GetMeasureLabel(MeasureFile file, bool newest)
        {
            var result = file.Result;
            var device = string.IsNullOrEmpty(result.DeviceModel) ? result.Platform : result.DeviceModel;
            return (newest ? "Newest: " : string.Empty) + $"{MeasureHistory.FormatStartedAt(result, TimeZoneInfo.Local)} · {device}" + (result.ProfilerCapture ? " · Profiler capture" : string.Empty) + $" · {result.Status}";
        }

        private static string GetPreviousLabel(MeasureResult result)
        {
            return MeasureHistory.FormatStartedAt(result, TimeZoneInfo.Local) + (string.IsNullOrEmpty(result.Commit) ? string.Empty : $" (commit {result.Commit})");
        }

        private void ShowMeasureMenu()
        {
            var menu = new GenericMenu();
            for (var i = 0; i < _files.Count; i++)
            {
                var file = _files[i];
                var path = i == 0 ? null : file.Path;
                var on = string.IsNullOrEmpty(_shownPath) ? i == 0 : _shownPath == file.Path;
                // A slash would make a submenu.
                menu.AddItem(new GUIContent(GetMeasureLabel(file, i == 0).Replace('/', '∕')), on, () =>
                {
                    _shownPath = path;
                    Refresh();
                });
            }

            if (_files.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No measure"));
            }

            menu.DropDown(_measureMenuButton.worldBound);
        }

        private void RevealShown()
        {
            if (_shown != null && File.Exists(_shown.Path))
            {
                EditorUtility.RevealInFinder(_shown.Path);
            }
        }

        private void UpdateSettingsFields()
        {
            _platformField.SetValueWithoutNotify(_settings.Platform == BuildTarget.iOS ? _platformChoices[1] : _platformChoices[0]);
            _runsField.SetValueWithoutNotify(_settings.Runs);
            _runsField.style.display = _settings.ProfilerCapture ? DisplayStyle.None : DisplayStyle.Flex;
            _captureField.SetValueWithoutNotify(_settings.ProfilerCapture);
            _captureSecondsField.SetValueWithoutNotify(_settings.ProfilerSeconds);
            _captureSecondsField.style.display = _settings.ProfilerCapture ? DisplayStyle.Flex : DisplayStyle.None;
            _sourceField.SetValueWithoutNotify(_sourceChoices[_settings.Rebuild ? SOURCE_NEW_BUILD : _settings.CleanInstall ? SOURCE_LAST_BUILD : SOURCE_INSTALLED_GAME]);
            _buildRow.style.display = _settings.Rebuild || _settings.CleanInstall ? DisplayStyle.Flex : DisplayStyle.None;
            _buildField.SetValueWithoutNotify(_settings.BuildPath ?? string.Empty);
            _buildField.tooltip = _settings.Platform == BuildTarget.iOS ? "The folder of the Xcode project, which the measure builds for the device with xcodebuild. Empty for the default folder." : "The APK or the AAB, after Build App Bundle. Empty for the default path.";
            _firstLaunchWaitField.SetValueWithoutNotify(_settings.FirstLaunchWait);
            _launchWaitField.SetValueWithoutNotify(_settings.LaunchWait);
            UpdateDevices();
            UpdateSettingsState();
        }

        /// <summary>
        ///     What may change outside the window: the active build target, the build on disk, a measure started elsewhere.
        /// </summary>
        private void UpdateSettingsState()
        {
            if (_measureButton == null) return;

            var running = StartupMeasure.IsRunning;
            var busy = running || _checking != null;
            var issue = StartupMeasure.GetBlockingIssue(_settings);
            _issueBox.text = issue ?? string.Empty;
            _issueBox.style.display = issue == null || busy ? DisplayStyle.None : DisplayStyle.Flex;
            _settingsPanel.SetEnabled(!busy);
            _measureButton.SetEnabled(issue == null && !busy);
            _measureButton.text = _checking != null ? "Checking the device…" : running ? "Measuring…" : _settings.ProfilerCapture ? "Capture" : "Measure";
            _buildField.textEdition.placeholder = MeasureBuild.GetDefaultPath(_settings.Platform, _settings.ProfilerCapture).Replace('\\', '/');
            UpdateProgress();
        }

        /// <summary>
        ///     The bar of the device check before a measure, then of the running measure: the steps done, the step under way
        ///     and the time it has taken.
        /// </summary>
        private void UpdateProgress()
        {
            var run = StartupMeasure.IsRunning ? StartupMeasure.Current : null;
            _progressRow.style.display = run != null || _checking != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (run != null)
            {
                var now = DateTime.UtcNow;
                _progressBar.highValue = run.StepCount;
                _progressBar.value = Math.Max(0, run.StepIndex);
                _progressBar.title = MeasureProgress.GetTitle(run.Result.Step, Math.Max(0, run.StepIndex), run.StepCount, now - run.StepStartedAt, now - run.StartedAt);
            }
            else if (_checking != null)
            {
                _progressBar.highValue = 1;
                _progressBar.value = 0;
                _progressBar.title = "Checking the device before the measure…";
            }
        }

        private void UpdateDevices()
        {
            var choices = new List<string> { _listing != null ? "Looking for devices…" : FIRST_AVAILABLE };
            _deviceIds = new List<string> { null };
            foreach (var device in _devices)
            {
                choices.Add($"{device.Name} ({device.Id})");
                _deviceIds.Add(device.Id);
            }

            var index = string.IsNullOrEmpty(_settings.Device) ? 0 : _deviceIds.IndexOf(_settings.Device);
            if (index < 0)
            {
                choices.Add($"{_settings.Device} (not found)");
                _deviceIds.Add(_settings.Device);
                index = choices.Count - 1;
            }

            _deviceField.choices = choices;
            _deviceField.SetValueWithoutNotify(choices[index]);
            _devicesButton.SetEnabled(_listing == null);
            _deviceBox.text = _deviceMessage ?? string.Empty;
            _deviceBox.style.display = _deviceMessage == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        ///     Lists the devices of the platform in the background. Not during a measure, not to slow its launches down.
        /// </summary>
        private async void RefreshDevices()
        {
            _listing?.Cancel();
            _listing = null;
            _devices = new List<ListedDevice>();
            _deviceMessage = null;
            var platform = _settings.Platform;
            if (StartupMeasure.IsRunning || (platform == BuildTarget.iOS && Application.platform != RuntimePlatform.OSXEditor))
            {
                UpdateDevices();
                return;
            }

            var listing = new CancellationTokenSource();
            _listing = listing;
            UpdateDevices();
            try
            {
                var devices = await StartupMeasure.ListDevicesAsync(platform, listing.Token);
                if (this == null || _listing != listing) return;

                _devices = devices;
                _deviceMessage = devices.Count > 0 ? null : platform == BuildTarget.iOS ? "No iOS device available: plug one in or put it on the network of this Mac, unlock it, trust this Mac and turn Developer Mode on, then refresh." : "No Android device ready: plug one in with USB debugging allowed, then refresh.";
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (MeasureException exception)
            {
                if (this == null || _listing != listing) return;

                _deviceMessage = exception.Message;
            }

            _listing = null;
            listing.Dispose();
            UpdateDevices();
        }

        private void BrowseBuild()
        {
            var chosen = ChooseBuild(StartupMeasure.GetBuildPath(_settings));
            if (string.IsNullOrEmpty(chosen)) return;

            _settings.BuildPath = MeasureBuild.ToProjectPath(chosen, Directory.GetCurrentDirectory());
            UpdateSettingsFields();
        }

        /// <summary>
        ///     Asks for the build: the folder of the Xcode project on iOS, where to save the APK or the AAB for a new build on
        ///     Android, and the APK or the AAB to install otherwise. Empty if cancelled.
        /// </summary>
        private string ChooseBuild(string current)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(current));
            if (_settings.Platform == BuildTarget.iOS) return EditorUtility.OpenFolderPanel("Xcode project of the measure", folder, string.Empty);
            if (_settings.Rebuild) return EditorUtility.SaveFilePanel("Build of the measure", folder, Path.GetFileNameWithoutExtension(current), EditorUserBuildSettings.buildAppBundle ? "aab" : "apk");

            return EditorUtility.OpenFilePanelWithFilters("Build to measure", folder, new[] { "Android builds", "aab,apk" });
        }

        /// <summary>
        ///     Checks the device and asks before a clean install, which deletes the data of the game on it, then starts the
        ///     measure on that device.
        /// </summary>
        private async void StartMeasure()
        {
            if (StartupMeasure.IsRunning || _checking != null || StartupMeasure.GetBlockingIssue(_settings) != null) return;

            var settings = _settings.Copy();
            if (settings.CleanInstall)
            {
                var checking = new CancellationTokenSource();
                _checking = checking;
                UpdateSettingsState();
                try
                {
                    var device = await StartupMeasure.CheckDeviceAsync(settings, checking.Token);
                    if (this == null) return;

                    var confirmation = MeasureChecks.GetConfirmation(settings, StartupMeasure.GetApplicationIdentifier(settings.Platform), device.Model, StartupMeasure.GetBuildPath(settings).Replace('\\', '/'));
                    if (!EditorUtility.DisplayDialog(TITLE, confirmation, settings.ProfilerCapture ? "Capture" : "Measure", "Cancel")) return;

                    settings.Device = device.Id;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (MeasureException exception)
                {
                    EditorUtility.DisplayDialog(TITLE, exception.Message, "OK");
                    return;
                }
                finally
                {
                    if (_checking == checking)
                    {
                        _checking = null;
                    }

                    checking.Dispose();
                    if (this != null)
                    {
                        UpdateSettingsState();
                    }
                }
            }

            if (StartupMeasure.IsRunning) return;

            StartupMeasure.Start(settings);
            _shownPath = null;
            _wasRunning = true;
            Refresh();
        }

        private void CancelMeasure()
        {
            if (_checking != null)
            {
                _checking.Cancel();
                return;
            }

            if (StartupMeasure.IsRunning)
            {
                StartupMeasure.Current.Cancel();
            }
        }
    }
}