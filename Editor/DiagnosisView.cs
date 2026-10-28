using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The Diagnosis tab of <see cref="StartupMeasureWindow" />: runs the diagnosis on demand, then shows its warnings
    ///     and infos, each with what is in cause and a link to its section of the guide, and the passed checks folded. With
    ///     a measure of the start, the checks of its longest step come first, and each step shows its share of the start.
    /// </summary>
    internal sealed class DiagnosisView : VisualElement
    {
        private const string SETTINGS_PATH = "Project/Startup Diagnostics";

        private static readonly Color _warningColor = new(0.93f, 0.6f, 0.17f);
        private static readonly Color _infoColor = new(0.27f, 0.52f, 0.93f);
        private static readonly Color _passedColor = new(0.36f, 0.72f, 0.36f);
        private static readonly Color _cardColor = new(0.5f, 0.5f, 0.5f, 0.08f);

        private readonly DiagnosisRun _run;
        private readonly Label _summary;
        private readonly HelpBox _emptyBox;
        private readonly Label _longestStep;
        private readonly ScrollView _results;

        /// <summary>
        ///     The view of a run that the window keeps across domain reloads: running the diagnosis again replaces its
        ///     results.
        /// </summary>
        public DiagnosisView(DiagnosisRun run)
        {
            _run = run;
            style.flexGrow = 1;

            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(Run) { text = "Run", tooltip = "Checks the settings of the project and the content of the first scenes. Loads the assets it checks." });
            _summary = new Label { style = { flexGrow = 1, flexShrink = 1, unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 6, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap } };
            toolbar.Add(_summary);
            toolbar.Add(new ToolbarButton(OpenSettings) { text = "Settings", tooltip = "The playable scenes, the thresholds and the check before builds, in Project Settings › Startup Diagnostics." });
            Add(toolbar);

            _emptyBox = new HelpBox("Run the diagnosis to check what weighs on the start: the settings of the project, and the content of the first scene of the build and of the playable scenes.", HelpBoxMessageType.Info) { style = { marginLeft = 4, marginRight = 4, marginTop = 6 } };
            Add(_emptyBox);
            _longestStep = new Label { style = { whiteSpace = WhiteSpace.Normal, marginLeft = 6, marginRight = 6, marginTop = 6, marginBottom = 2 } };
            Add(_longestStep);
            _results = new ScrollView { style = { flexGrow = 1 } };
            Add(_results);
            Show();
        }

        public bool HasRun => !string.IsNullOrEmpty(_run.RunAt);

        /// <summary>
        ///     Runs the diagnosis and shows its results.
        /// </summary>
        public void Run()
        {
            var run = StartupDiagnosis.Run();
            _run.RunAt = run.RunAt;
            _run.Results = run.Results;
            Show();
        }

        /// <summary>
        ///     Shows the results again, in the order that the newest measure gives.
        /// </summary>
        public void Refresh()
        {
            Show();
        }

        private void Show()
        {
            _results.Clear();
            _emptyBox.style.display = HasRun ? DisplayStyle.None : DisplayStyle.Flex;
            _longestStep.style.display = HasRun ? DisplayStyle.Flex : DisplayStyle.None;
            _summary.text = HasRun ? $"{DiagnosisReport.GetSummary(_run.Results)}, checked {FormatRunAt(_run.RunAt)}" : string.Empty;
            if (!HasRun) return;

            var times = StepTimes.FromNewest(MeasureHistory.Load(StartupMeasure.RESULTS_FOLDER), EditorUserBuildSettings.activeBuildTarget.ToString());
            _longestStep.text = DiagnosisReport.DescribeLongestStep(times, times == null ? null : MeasureHistory.FormatStartedAt(times.Measure, TimeZoneInfo.Local));
            var passed = new Foldout { value = false, style = { marginLeft = 4, marginRight = 4, marginTop = 6 } };
            var passedCount = 0;
            foreach (var result in DiagnosisReport.Sort(_run.Results, times?.Longest))
            {
                if (result.Level == DiagnosisLevel.Passed)
                {
                    passed.Add(CreateCard(result, times));
                    passedCount++;
                }
                else
                {
                    _results.Add(CreateCard(result, times));
                }
            }

            if (passedCount == 0) return;

            passed.text = $"Passed ({passedCount})";
            _results.Add(passed);
        }

        private static VisualElement CreateCard(DiagnosisResult result, StepTimes times)
        {
            var color = GetColor(result.Level);
            var card = new VisualElement { style = { marginLeft = 4, marginRight = 4, marginTop = 4, paddingLeft = 6, paddingRight = 4, paddingTop = 3, paddingBottom = 5, backgroundColor = _cardColor, borderLeftWidth = 3, borderLeftColor = color } };
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            header.Add(new Label(GetLevelName(result.Level)) { style = { color = color, unityFontStyleAndWeight = FontStyle.Bold, minWidth = 56 } });
            header.Add(new Label(result.Title) { enableRichText = false, style = { flexGrow = 1, flexShrink = 1, unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal } });
            var isLongest = times != null && result.Step == times.Longest;
            header.Add(new Label(DiagnosisReport.GetStepLabel(result.Step, times)) { tooltip = DiagnosisReport.GetStepRange(result.Step), style = { opacity = isLongest ? 1 : 0.7f, unityFontStyleAndWeight = isLongest ? FontStyle.Bold : FontStyle.Normal, marginRight = 4 } });
            var url = DiagnosisGuide.GetUrl(result.GuideSection);
            header.Add(new Button(() => Application.OpenURL(url)) { text = "Guide", tooltip = $"Opens the section of the optimization guide about this check: {url}" });
            card.Add(header);
            card.Add(new Label(result.Message) { enableRichText = false, style = { whiteSpace = WhiteSpace.Normal, marginTop = 2 } });
            foreach (var item in result.Items)
            {
                card.Add(CreateItem(item));
            }

            return card;
        }

        private static Label CreateItem(DiagnosisItem item)
        {
            var text = string.IsNullOrEmpty(item.Detail) ? item.Label : $"{item.Label} — {item.Detail}";
            var label = new Label($"• {text}") { enableRichText = false, style = { whiteSpace = WhiteSpace.Normal, marginLeft = 8, marginTop = 1 } };
            if (string.IsNullOrEmpty(item.AssetPath)) return label;

            label.tooltip = $"Selects {item.AssetPath} in the Project window.";
            label.style.color = _infoColor;
            label.RegisterCallback<ClickEvent>(_ => SelectAsset(item.AssetPath));
            return label;
        }

        private static void SelectAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(SETTINGS_PATH);
        }

        private static Color GetColor(DiagnosisLevel level)
        {
            return level switch
            {
                DiagnosisLevel.Warning => _warningColor,
                DiagnosisLevel.Info => _infoColor,
                _ => _passedColor
            };
        }

        private static string GetLevelName(DiagnosisLevel level)
        {
            return level switch
            {
                DiagnosisLevel.Warning => "Warning",
                DiagnosisLevel.Info => "Info",
                _ => "Passed"
            };
        }

        /// <summary>
        ///     When the diagnosis ran: its time today, its date and time before.
        /// </summary>
        private static string FormatRunAt(string runAt)
        {
            if (!DateTime.TryParse(runAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) return runAt;

            return date.Date == DateTime.Today ? $"at {date:HH:mm}" : $"on {date:yyyy-MM-dd HH:mm}";
        }
    }
}