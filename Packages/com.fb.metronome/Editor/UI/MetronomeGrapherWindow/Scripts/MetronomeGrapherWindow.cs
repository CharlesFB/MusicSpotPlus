using FB.Metronome.Data;
using FB.Metronome.Editor.Analysis;
using FB.Metronome.Editor.Preview;
using UnityEngine.Scripting.APIUpdating;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FB.Metronome.Editor.UI
{
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "MetronomeEditor", sourceClassName: "MetronomeGrapherWindow")]
    public partial class MetronomeGrapherWindow : EditorWindow
    {
        private static string UiPath
        {
            get
            {
                string script = AssetDatabase.GUIDToAssetPath("fdb87a70d5e25404dbc20e9ea3cb6762");
                return System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(script)).Replace('\\', '/') + "/";
            }
        }
        [SerializeField] private VisualTreeAsset metronomeWindow;
        [SerializeField] private VisualTreeAsset laneItemUxml;
        [SerializeField] private VisualTreeAsset EventItemUxml;
        [SerializeField] private MetroSource _source;
        [SerializeField] private bool _sourceLocked;
        [SerializeField] private List<EventLane> _laneSlots = new List<EventLane> { null };
        [SerializeField] private EventLane _currentLane;
        [SerializeField] private int _selectedIndex = -1;
        [SerializeField] private double _seekSeconds;
        [SerializeField] private bool _snapEnabled = true;

        private ListView _lvLanes;
        private ScrollView _eventScroll, _spectrumScroll;
        private EventStripView _eventStrip;
        private ObjectField _sourceField;
        private SpectrumTrack _track;
        private Button _btnDeleteEvent, _btnApplyChange, _btnAddEvent, _btnAnalyze, _btnPlay, _btnPause, _btnStop;
        private TextField _tfIndex, _tfEventId, _tfTime, _tfPayload, _tfSeek, _tfBpm, _tfOffset, _tfDuration;
        private Toggle _snap;
        private DropdownField _meter;
        private IntegerField _subdivision;
        private HelpBox _status;
        private MetroEvent _selectedEvent;
        private AudioClip _clip, _cachedClip;
        private Hash128 _cachedHash;
        private float[] _envelope = Array.Empty<float>();
        private bool _waveformPending, _analyzePending, _syncingLanes, _guiReady, _previewRunning;
        private double _loadDeadline, _previewStarted;
        private MetroEditorPreview _preview;

        private int _dragUndoGroup = -1;
        private MetroEvent _clipboard;
        [SerializeField] private float _zoom = 100;
        private float Duration => _source == null ? 0 : Mathf.Max(0, _source.clip != null ? _source.clip.length :
            TimelineMath.IsFinite(_source.duration) ? _source.duration : 0);

        [MenuItem("Tools/Metronome/Timeline")]
        public static void ShowExample() => OpenWindow();

        private static MetronomeGrapherWindow OpenWindow()
        {
            var window = GetWindow<MetronomeGrapherWindow>();
            window.titleContent = new GUIContent("Metronome Grapher");
            window.minSize = new Vector2(1050, 800);
            return window;
        }

        public static void ShowMetronomeTimeline(MetroSource source)
        {
            var window = OpenWindow();
            window.BindSource(source, true);
            window.Focus();
        }

        public static void ShowLane(EventLane lane)
        {
            if (lane == null || lane.source == null) return;
            var window = OpenWindow();
            window.BindSource(lane.source, true);
            window.NormalizeLaneSlots();
            if (!window._laneSlots.Contains(lane)) window._laneSlots.Insert(window._laneSlots.Count - 1, lane);
            window._currentLane = lane;
            window._selectedEvent = null;
            window._selectedIndex = -1;
            if (window._guiReady) { window.RefreshLaneList(); window.RefreshEvents(); window.RefreshGrid(); }
            window.Focus();
        }

        private void OnEnable()
        {
            _preview = new MetroEditorPreview();
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += EditorTick;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            _preview?.Dispose();
            _previewRunning = false;
            _guiReady = false;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= EditorTick;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        public void CreateGUI()
        {
            _guiReady = false;
            rootVisualElement.Clear();
            if (metronomeWindow == null) metronomeWindow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "MetronomeGrapherWindow.uxml");
            if (laneItemUxml == null) laneItemUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "LaneItem.uxml");
            if (EventItemUxml == null) EventItemUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "EventItem.uxml");
            if (metronomeWindow == null || laneItemUxml == null || EventItemUxml == null)
            {
                rootVisualElement.Add(new HelpBox("缺少窗口 UXML / LaneItem / EventItem 模板，请恢复资源引用。", HelpBoxMessageType.Error));
                return;
            }
            var root = metronomeWindow.CloneTree();
            root.style.flexGrow = 1;
            rootVisualElement.Add(root);
            _track = root.Q<SpectrumTrack>("spectrumTrack");
            _spectrumScroll = root.Q<ScrollView>("svSpectrumWindow");
            _spectrumScroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            _spectrumScroll.horizontalScroller.valueChanged += _ => RefreshVisibleRange();
            _spectrumScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => RefreshVisibleRange());
            _track.SeekChanged += time => SetSeek(time, true);
            _track.EventSelected += e => SelectEvent(e, true);
            _track.EventDragged += MoveMarker;
            _track.SetZoom(_zoom);
            root.RegisterCallback<KeyDownEvent>(OnShortcut);

            _sourceField = new ObjectField { objectType = typeof(MetroSource), allowSceneObjects = false };
            _sourceField.style.flexGrow = 1;
            _sourceField.RegisterValueChangedCallback(e => BindSource(e.newValue as MetroSource, false));
            root.Q<VisualElement>("SourceContainer").Add(_sourceField);

            // === 每次 CreateGUI 都重新查询控件，避免引用已经拆下的旧树 ===
            _tfIndex = root.Q<TextField>("tfIndex");
            _tfEventId = root.Q<TextField>("tfEventId");
            _tfTime = root.Q<TextField>("tfTime");
            _tfPayload = root.Q<TextField>("tfPayload");
            _tfSeek = root.Q<TextField>("tfSeek");
            _tfBpm = root.Q<TextField>("tfBpm");
            _tfOffset = root.Q<TextField>("tfOffset");
            _tfDuration = root.Q<TextField>("tfDuration");
            _tfIndex.isReadOnly = _tfDuration.isReadOnly = true;
            foreach (var field in new[] { _tfEventId, _tfTime, _tfPayload, _tfSeek, _tfBpm, _tfOffset })
                field.pickingMode = PickingMode.Position;
            _tfSeek.isDelayed = _tfBpm.isDelayed = _tfOffset.isDelayed = true;
            _tfEventId.RegisterValueChangedCallback(_ => RefreshApplyState());
            _tfTime.RegisterValueChangedCallback(_ => RefreshApplyState());
            _tfPayload.RegisterValueChangedCallback(_ => RefreshApplyState());
            _tfSeek.RegisterValueChangedCallback(_ => CommitSeekField());
            _tfBpm.RegisterValueChangedCallback(_ => ApplySourceSettings());
            _tfOffset.RegisterValueChangedCallback(_ => ApplySourceSettings());
            _btnAddEvent = root.Q<Button>("btnAddEvent");
            _btnApplyChange = root.Q<Button>("btnApply");
            _btnDeleteEvent = root.Q<Button>("btnDeleteEvent");
            _btnAddEvent.clicked += AddEvent;
            _btnApplyChange.clicked += ApplyEventChange;
            _btnDeleteEvent.clicked += DeleteEvent;

            _eventScroll = root.Q<ScrollView>("lvEventContainer");
            _eventStrip = new EventStripView(_eventScroll, EventItemUxml, e => SelectEvent(e, false));
            SetupToolbar(root.Q<VisualElement>("timelineToolbar"));
            SetupLaneList(root);
            _guiReady = true;
            // MetroEvent 是内联序列化 class，域重载后从 Lane 列表恢复，避免另一份拷贝。
            _selectedEvent = _currentLane != null && _currentLane.events != null && _selectedIndex >= 0 &&
                             _selectedIndex < _currentLane.events.Count ? _currentLane.events[_selectedIndex] : null;
            SetupSpectrum();
            RefreshEvents();
        }

        private void SetupToolbar(VisualElement toolbar)
        {
            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.flexWrap = Wrap.Wrap;
            toolbar.Add(controls);
            var zoom = new Slider("缩放", 20, 500) { value = _zoom };
            zoom.style.width = 150;
            zoom.RegisterValueChangedCallback(e =>
            {
                float timeAtLeft = _spectrumScroll.scrollOffset.x / _track.PixelsPerSecond;
                _zoom = e.newValue; _track.SetZoom(_zoom);
                _spectrumScroll.scrollOffset = new Vector2(timeAtLeft * _zoom, 0);
                RefreshVisibleRange();
            });
            controls.Add(zoom);
            _btnAnalyze = new Button(AnalyzeBpm) { text = "Analyze BPM" };
            _meter = new DropdownField("拍号", new List<string> { "2/2", "3/4", "4/4", "6/8" }, 2);
            _meter.tooltip = "BPM 表示每分钟分母音符的拍数。改拍号不移动已有事件；6/8 在此表示六个八分音符拍。";
            _meter.style.width = 210;
            _meter.RegisterValueChangedCallback(_ => ApplyMeter());
            _snap = new Toggle("吸附") { value = _snapEnabled };
            _snap.RegisterValueChangedCallback(e => _snapEnabled = e.newValue);
            _subdivision = new IntegerField("每拍细分") { isDelayed = true };
            _subdivision.style.width = 210;
            _subdivision.tooltip = "1=一拍一格；3=三连音；4=四等分。只影响之后的添加/时间修改。";
            _subdivision.RegisterValueChangedCallback(e =>
            {
                if (_currentLane == null) return;
                Undo.RecordObject(_currentLane, "Change Lane Subdivision");
                _currentLane.snapSubdivision = Mathf.Clamp(e.newValue, 1, 64);
                EditorUtility.SetDirty(_currentLane);
                RefreshGrid();
            });
            _btnPlay = new Button(PlayPreview) { text = "Play / Resume" };
            _btnPause = new Button(PausePreview) { text = "Pause" };
            _btnStop = new Button(() => { StopPreview(); SetSeek(0); }) { text = "Stop" };
            foreach (var element in new VisualElement[] { _btnAnalyze, _meter, _snap, _subdivision, _btnPlay, _btnPause, _btnStop })
                controls.Add(element);
            _status = new HelpBox("", HelpBoxMessageType.Info);
            _status.style.display = DisplayStyle.None;
            toolbar.Add(_status);
        }

        private void BindSource(MetroSource source, bool lockField)
        {
            if (_source != source)
            {
                StopPreview();
                _source = source;
                _laneSlots = new List<EventLane> { null };
                _currentLane = null;
                _selectedEvent = null;
                _selectedIndex = -1;
                _seekSeconds = 0;
                _analyzePending = false;
            }
            _sourceLocked = lockField && source != null;
            if (!_guiReady) return;
            RefreshLaneList();
            SetupSpectrum();
            RefreshEvents();
        }

        private void NormalizeLaneSlots()
        {
            if (_laneSlots == null) _laneSlots = new List<EventLane>();
            var seen = new HashSet<EventLane>();
            _laneSlots.RemoveAll(lane => lane == null || _source == null || lane.source != _source || !seen.Add(lane));
            if (!_laneSlots.Contains(_currentLane)) { _currentLane = null; _selectedEvent = null; _selectedIndex = -1; }
            _laneSlots.Add(null);
        }

        private void SetupLaneList(VisualElement root)
        {
            NormalizeLaneSlots();
            _lvLanes = root.Q<ListView>("lvLaneContainer");
            _lvLanes.fixedItemHeight = 30;
            _lvLanes.selectionType = SelectionType.Single;
            _lvLanes.makeItem = () =>
            {
                var item = new LaneItem(laneItemUxml);
                item.LaneAssigned += OnLaneAssigned;
                item.LaneRemoved += OnLaneRemoved;
                return item;
            };
            _lvLanes.bindItem = (element, index) => ((LaneItem)element).Bind(_laneSlots[index], index);
            _lvLanes.selectionChanged += _ =>
            {
                if (_syncingLanes || !_guiReady) return;
                int index = _lvLanes.selectedIndex;
                var lane = index >= 0 && index < _laneSlots.Count ? _laneSlots[index] : null;
                if (_currentLane != lane) { _currentLane = lane; _selectedEvent = null; _selectedIndex = -1; }
                RefreshEvents();
                RefreshGrid();
            };
            RefreshLaneList();
        }

        private void RefreshLaneList()
        {
            if (_lvLanes == null) return;
            NormalizeLaneSlots();
            _syncingLanes = true;
            try
            {
                _lvLanes.itemsSource = _laneSlots;
                _lvLanes.Rebuild();
                if (_currentLane != null) _lvLanes.SetSelectionWithoutNotify(new[] { _laneSlots.IndexOf(_currentLane) });
                else _lvLanes.SetSelectionWithoutNotify(Array.Empty<int>());
            }
            finally { _syncingLanes = false; }
        }

        private void OnLaneAssigned(int index, EventLane lane)
        {
            if (index < 0 || index >= _laneSlots.Count) return;
            if (_source == null || lane == null || lane.source != _source || _laneSlots.Contains(lane))
            {
                ShowStatus("请拖入引用当前 Source 的 Lane，且不能重复。", true);
                _lvLanes.RefreshItem(index);
                return;
            }
            _laneSlots[index] = lane;
            _currentLane = lane;
            _selectedEvent = null;
            _selectedIndex = -1;
            RefreshLaneList();
            RefreshEvents();
            RefreshGrid();
        }

        private void OnLaneRemoved(int index)
        {
            if (index < 0 || index >= _laneSlots.Count || _laneSlots[index] == null) return;
            var removed = _laneSlots[index];
            _laneSlots.RemoveAt(index);
            if (_currentLane == removed) { _currentLane = null; _selectedEvent = null; _selectedIndex = -1; }
            RefreshLaneList();
            RefreshEvents();
            RefreshGrid();
        }

        // === 资源与波形缓存：打开/Undo 不覆盖手动 BPM；异步加载完成后再读 PCM ===
        private void SetupSpectrum()
        {
            if (!_guiReady) return;
            _sourceField.SetValueWithoutNotify(_source);
            _sourceField.SetEnabled(!_sourceLocked);
            var nextClip = _source != null ? _source.clip : null;
            if (_clip != nextClip) { StopPreview(); _analyzePending = false; }
            _clip = nextClip;
            _waveformPending = false;
            if (_clip == null) { _cachedClip = null; _envelope = Array.Empty<float>(); }
            else
            {
                var hash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(_clip));
                if (_cachedClip != _clip || _cachedHash != hash)
                {
                    _cachedClip = null;
                    _cachedHash = hash;
                    _envelope = Array.Empty<float>();
                    _waveformPending = true;
                    _loadDeadline = EditorApplication.timeSinceStartup + 30;
                    _clip.LoadAudioData();
                    TryLoadWaveform();
                }
            }
            _track.InitData(_envelope, Duration);
            SetSeek(_seekSeconds);
            FillSpectrumInfo();
            RefreshGrid();
            RefreshVisibleRange();
            RefreshPreviewButtons();
        }

        private void TryLoadWaveform()
        {
            if (!_waveformPending || _clip == null) return;
            if (_clip.loadState == AudioDataLoadState.Loading && EditorApplication.timeSinceStartup < _loadDeadline) return;
            _waveformPending = false;
            if (_clip.loadState != AudioDataLoadState.Loaded)
            {
                _analyzePending = false;
                ShowStatus("音频数据加载失败或超时，请检查 AudioClip 导入设置。", true);
                return;
            }
            int bins = Mathf.Clamp(Mathf.CeilToInt(_clip.length * 100), 1, 65536);
            _envelope = AudioClipUtils.GetClipEnvelope(_clip, bins);
            _cachedClip = _clip;
            _track.InitData(_envelope, Duration);
            if (_envelope.Length == 0) ShowStatus("无法读取 PCM，请使用 Decompress On Load；仍可在空轨上编辑事件。", true);
            if (_analyzePending) { _analyzePending = false; AnalyzeBpm(); }
        }

        private void FillSpectrumInfo()
        {
            _tfBpm.SetValueWithoutNotify(_source != null ? Number(_source.bpm) : "");
            _tfOffset.SetValueWithoutNotify(_source != null ? Number(_source.beatOffset) : "");
            _tfDuration.SetValueWithoutNotify($"{(int)Duration / 60}:{(int)Duration % 60:00} ({Duration * 1000:0} ms)");
            _tfBpm.SetEnabled(_source != null);
            _tfOffset.SetEnabled(_source != null);
        }

        private void ApplySourceSettings()
        {
            if (_source == null) return;
            if (!TimelineMath.TrySeconds(_tfBpm.value, out float bpm) || bpm <= 0 || bpm > 1000 ||
                !TimelineMath.TrySeconds(_tfOffset.value, out float offset) || Mathf.Abs(offset) > 1000000)
            {
                ShowStatus("BPM 应在 (0, 1000]，Offset 应为有限秒数（绝对值不超过 1000000）。", true);
                FillSpectrumInfo();
                return;
            }
            Undo.RecordObject(_source, "Edit Timeline Settings");
            _source.bpm = bpm;
            _source.beatOffset = offset;
            EditorUtility.SetDirty(_source);
            FillSpectrumInfo();
            RefreshGrid();
        }

        private void AnalyzeBpm()
        {
            if (_source == null || _clip == null) return;
            if (_waveformPending) { _analyzePending = true; ShowStatus("等待音频加载后分析 BPM…"); return; }
            if (_envelope.Length == 0 || Array.TrueForAll(_envelope, x => x <= 0))
            {
                ShowStatus("没有可分析的 PCM 数据（静音或读取失败），保留当前 BPM。", true);
                return;
            }
            Undo.RecordObject(_source, "Analyze MetroSource");
            _source.AnalyzeFromClip();
            EditorUtility.SetDirty(_source);
            FillSpectrumInfo();
            RefreshGrid();
            ShowStatus("BPM 已估算，可手动修正；已有事件位置保持不变。");
        }

        private void ApplyMeter()
        {
            if (_source == null) return;
            string[] parts = _meter.value.Split('/');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int count) || !int.TryParse(parts[1], out int unit)) return;
            Undo.RecordObject(_source, "Change Time Signature");
            _source.beatsPerMeasure = count;
            _source.beatUnit = unit;
            EditorUtility.SetDirty(_source);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            if (_meter == null) return;
            string meter = _source == null ? "4/4" : $"{_source.beatsPerMeasure}/{_source.beatUnit}";
            if (!_meter.choices.Contains(meter)) _meter.choices.Add(meter);
            _meter.SetValueWithoutNotify(meter);
            _meter.SetEnabled(_source != null);
            int subdivision = _currentLane != null ? Mathf.Clamp(_currentLane.snapSubdivision, 1, 64) : 4;
            _subdivision.SetValueWithoutNotify(subdivision);
            _subdivision.SetEnabled(_currentLane != null);
            _track.SetGrid(_source != null ? _source.bpm : 120, _source != null ? _source.beatOffset : 0,
                _source != null ? _source.beatsPerMeasure : 4, subdivision);
        }

        private void RefreshVisibleRange()
        {
            if (_track == null || _spectrumScroll == null) return;
            float width = _spectrumScroll.contentViewport.layout.width;
            if (TimelineMath.IsFinite(width) && width > 0) _track.SetVisibleRange(_spectrumScroll.scrollOffset.x, width);
        }

        private bool CommitSeekField()
        {
            if (!TimelineMath.TrySeconds(_tfSeek.value, out float time))
            {
                ShowStatus("Seek 必须是有限秒数。", true);
                _tfSeek.SetValueWithoutNotify(Number((float)_seekSeconds));
                return false;
            }
            SetSeek(time, true);
            return true;
        }

        private void SetSeek(double time, bool seekAudio = false, bool updateField = true)
        {
            _seekSeconds = TimelineMath.Clamp(time, Duration);
            if (updateField) _tfSeek?.SetValueWithoutNotify(Number((float)_seekSeconds));
            _track?.SetSeekSeconds((float)_seekSeconds);
            if (_guiReady) RefreshPreviewButtons();
            if (seekAudio && _previewRunning)
            {
                if (_seekSeconds >= Duration) StopPreview();
                else StartPreviewAtSeek();
            }
        }

        private void PlayPreview()
        {
            if (_clip == null || EditorApplication.isPlayingOrWillChangePlaymode || _previewRunning || !CommitSeekField()) return;
            if (_seekSeconds >= Duration) SetSeek(0);
            StartPreviewAtSeek();
        }

        private void StartPreviewAtSeek()
        {
            _previewRunning = _preview != null && _preview.Play(_clip, (float)_seekSeconds);
            _previewStarted = EditorApplication.timeSinceStartup;
            if (!_previewRunning) ShowStatus("当前 Unity 音频预览不可用，请检查音频设置。", true);
            RefreshPreviewButtons();
        }

        private void PausePreview()
        {
            if (!_previewRunning) return;
            try { SetSeek(_preview.Position); }
            catch (Exception e) { ShowStatus(e.GetBaseException().Message, true); }
            StopPreview();
        }

        private void StopPreview()
        {
            _preview?.Stop();
            _previewRunning = false;
            if (_guiReady) RefreshPreviewButtons();
        }

        private void RefreshPreviewButtons()
        {
            bool available = _clip != null && _preview != null && _preview.Available && !EditorApplication.isPlayingOrWillChangePlaymode;
            _btnPlay?.SetEnabled(available && !_previewRunning);
            _btnPause?.SetEnabled(_previewRunning);
            _btnStop?.SetEnabled(_previewRunning || _seekSeconds > 0);
            _btnAnalyze?.SetEnabled(_clip != null);
        }

        private void EditorTick()
        {
            if (!_guiReady) return;
            TryLoadWaveform();
            if (!_previewRunning) return;
            try
            {
                if (_preview.IsPlaying)
                {
                    // 试听时允许输入新的 Seek，进度刷新不覆盖正在输入的草稿。
                    var focused = _tfSeek.focusController?.focusedElement as VisualElement;
                    bool editingSeek = focused != null && (focused == _tfSeek || _tfSeek.Contains(focused));
                    SetSeek(_preview.Position, updateField: !editingSeek);
                }
                else if (EditorApplication.timeSinceStartup - _previewStarted > 0.25)
                {
                    // 自然结束停在片尾；被其他预览中断则保留最近读到的位置。
                    if (_seekSeconds >= Duration - 0.15f) SetSeek(Duration);
                    if (_preview.LostOwnership) ShowStatus("试听通道被其他窗口抢占，已保留编谱位置。", true);
                    StopPreview();
                }
            }
            catch (Exception e) { StopPreview(); ShowStatus("预览失败：" + e.GetBaseException().Message, true); }
            RefreshPreviewButtons();
        }

        private void MoveMarker(MetroEvent e, float time, bool finished)
        {
            if (_currentLane == null) return;
            if (_dragUndoGroup < 0)
            {
                Undo.IncrementCurrentGroup(); _dragUndoGroup = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(_currentLane, "Move MetroEvent");
                _selectedEvent = e;
            }
            e.time = EventTime(time); _currentLane.Renumber();
            EditorUtility.SetDirty(_currentLane);
            _track.SetEvents(_currentLane.events, e);
            RefreshEventInfo();
            if (finished) { Undo.CollapseUndoOperations(_dragUndoGroup); _dragUndoGroup = -1; RefreshEvents(); }
        }
        private void OnShortcut(KeyDownEvent evt)
        {
            var focused = rootVisualElement.focusController.focusedElement as VisualElement;
            if (focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null) return;
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) DeleteEvent();
            else if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.C && _selectedEvent != null)
                _clipboard = new MetroEvent { eventId = _selectedEvent.eventId, payload = _selectedEvent.payload };
            else if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.V && _clipboard != null && _currentLane != null)
            {
                Undo.RecordObject(_currentLane, "Paste MetroEvent");
                _selectedEvent = _currentLane.AddEvent(EventTime((float)_seekSeconds), _clipboard.eventId, false, _clipboard.payload);
                EditorUtility.SetDirty(_currentLane); RefreshEvents();
            }
            else return;
            evt.StopPropagation();
        }

        private void OnUndoRedo()
        {
            if (!_guiReady) return;
            // Undo 可能替换整个 events 列表，旧 MetroEvent 引用不能继续写入。
            _selectedEvent = null;
            _selectedIndex = -1;
            RefreshLaneList();
            SetupSpectrum();
            RefreshEvents();
        }

        private void OnProjectChanged()
        {
            if (!_guiReady) return;
            var selected = _selectedEvent;
            string id = _tfEventId.value, time = _tfTime.value, payload = _tfPayload.value;
            bool draft = _btnApplyChange.enabledSelf;
            RefreshLaneList(); SetupSpectrum(); RefreshEvents();
            if (draft && selected != null && selected == _selectedEvent)
            {
                _tfEventId.SetValueWithoutNotify(id); _tfTime.SetValueWithoutNotify(time);
                _tfPayload.SetValueWithoutNotify(payload); RefreshApplyState();
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            StopPreview();
            if (_guiReady) RefreshPreviewButtons();
        }

        private void ShowStatus(string text, bool error = false)
        {
            if (_status == null) return;
            _status.text = text;
            _status.messageType = error ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info;
            _status.style.display = DisplayStyle.Flex;
        }

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
