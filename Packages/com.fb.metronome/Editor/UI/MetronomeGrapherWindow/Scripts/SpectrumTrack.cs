using FB.Metronome.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FB.Metronome.Editor.UI
{
    public class SpectrumTrack : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SpectrumTrack, UxmlTraits> { }
        public event Action<float> SeekChanged;
        public event Action<MetroEvent> EventSelected;

        public float PixelsPerSecond { get; private set; } = 100f;
        public float TimeToPixel(float seconds) => seconds * PixelsPerSecond;
        public event Action<MetroEvent, float, bool> EventDragged;
        private MetroEvent _dragEvent;
        private Vector2 _dragStart;
        private bool _dragMoved, _suppressClick;
        private readonly List<Label> _rulerLabels = new List<Label>();
        public void SetZoom(float pixelsPerSecond)
        {
            PixelsPerSecond = Mathf.Clamp(pixelsPerSecond, 20, 500);
            style.width = Mathf.Max(1, _duration * PixelsPerSecond);
            SetSeekSeconds(_seek); UpdateRuler(); MarkDirtyRepaint();
        }
        private const float MarkerY = 13f;
        private float[] _envelope = Array.Empty<float>();
        private readonly List<MetroEvent> _events = new List<MetroEvent>();
        private MetroEvent _selected;
        private float _duration, _seek, _bpm = 120f, _offset;
        private int _beatsPerMeasure = 4, _subdivision = 4;
        private float _viewX, _viewWidth = float.PositiveInfinity;
        private readonly VisualElement _playhead;

        public SpectrumTrack()
        {
            AddToClassList("spectrum-track");
            style.height = 250;
            style.flexShrink = 0;
            pickingMode = PickingMode.Position;
            // === 播放头单独移动，预览每帧更新不再重画整个波形 ===
            _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _playhead.style.position = Position.Absolute;
            _playhead.style.top = 0;
            _playhead.style.bottom = 0;
            _playhead.style.width = 2;
            _playhead.style.backgroundColor = new Color(1f, .42f, .42f);
            Add(_playhead);
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _dragEvent = HitTest(evt.localPosition); _dragStart = evt.localPosition; _dragMoved = false;
                if (_dragEvent != null) this.CapturePointer(evt.pointerId);
            });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_dragEvent != null && _dragMoved)
                { EventDragged?.Invoke(_dragEvent, TimelineMath.Clamp(evt.localPosition.x / PixelsPerSecond, _duration), true); _suppressClick = true; }
                _dragEvent = null; this.ReleasePointer(evt.pointerId);
            });
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_dragEvent != null && _dragMoved) EventDragged?.Invoke(_dragEvent, _dragEvent.time, true);
                _dragEvent = null;
            });
            RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (_dragEvent != null && ((Vector2)evt.localPosition - _dragStart).sqrMagnitude > 9)
                { _dragMoved = true; EventDragged?.Invoke(_dragEvent, TimelineMath.Clamp(evt.localPosition.x / PixelsPerSecond, _duration), false); }
                var hit = HitTest(evt.localPosition);
                tooltip = hit == null ? string.Empty : $"#{hit.index} {hit.eventId}  {hit.time:0.###}s";
            });
        }

        public void InitData(float[] data, float duration)
        {
            _envelope = data ?? Array.Empty<float>();
            _duration = TimelineMath.IsFinite(duration) ? Mathf.Max(0, duration) : 0;
            style.width = Mathf.Max(1, _duration * PixelsPerSecond);
            SetSeekSeconds(_seek);
            MarkDirtyRepaint();
        }

        public void SetGrid(float bpm, float offset, int beatsPerMeasure, int subdivision)
        {
            _bpm = TimelineMath.IsFinite(bpm) && bpm > 0 ? bpm : 120;
            _offset = TimelineMath.IsFinite(offset) ? offset : 0;
            _beatsPerMeasure = Mathf.Clamp(beatsPerMeasure, 1, 32);
            _subdivision = Mathf.Clamp(subdivision, 1, 64);
            MarkDirtyRepaint();
        }

        public void SetEvents(IEnumerable<MetroEvent> events, MetroEvent selected)
        {
            _events.Clear();
            if (events != null)
                foreach (var e in events)
                    if (e != null && TimelineMath.IsFinite(e.time) && e.time >= 0 && e.time <= _duration)
                        _events.Add(e);
            // 不修改资产顺序；相同时间按 index 排列，以便重叠点轮选。
            _events.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.index.CompareTo(b.index));
            SetSelected(selected);
        }

        public void SetSelected(MetroEvent selected) { _selected = selected; MarkDirtyRepaint(); }
        public void SetVisibleRange(float x, float width)
        {
            _viewX = Mathf.Max(0, x);
            _viewWidth = Mathf.Max(1, width);
            UpdateRuler();
            MarkDirtyRepaint();
        }
        public void UpdateProgress(float progress) => SetSeekSeconds(progress * _duration);
        public void SetSeekSeconds(float seconds)
        {
            _seek = TimelineMath.Clamp(seconds, _duration);
            _playhead.style.left = Mathf.Max(0, _seek * PixelsPerSecond - 1);
        }

        private void OnClick(ClickEvent evt)
        {
            if (evt.button != 0) return;
            if (_suppressClick) { _suppressClick = false; return; }
            var hit = HitTest(evt.localPosition);
            if (hit != null) EventSelected?.Invoke(hit);
            else
            {
                float time = TimelineMath.Clamp(evt.localPosition.x / PixelsPerSecond, _duration);
                SetSeekSeconds(time);
                SeekChanged?.Invoke(time);
            }
        }

        // === 命中区域只在顶部 marker 带内；重叠 marker 重复点击可轮选 ===
        public MetroEvent HitTest(Vector2 position)
        {
            if (Mathf.Abs(position.y - MarkerY) > 10) return null;
            int first = LowerBound((position.x - 8) / PixelsPerSecond);
            MetroEvent firstHit = null;
            bool afterSelected = false;
            for (int i = first; i < _events.Count && _events[i].time * PixelsPerSecond <= position.x + 8; i++)
            {
                var e = _events[i];
                if (firstHit == null) firstHit = e;
                if (afterSelected) return e;
                if (e == _selected) afterSelected = true;
            }
            return firstHit;
        }

        private int LowerBound(float time)
        {
            int low = 0, high = _events.Count;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (_events[mid].time < time) low = mid + 1;
                else high = mid;
            }
            return low;
        }

        private void UpdateRuler()
        {
            if (float.IsInfinity(_viewWidth)) return;
            float interval = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(80 / PixelsPerSecond)));
            while (interval * PixelsPerSecond < 80) interval *= 2;
            float first = Mathf.Ceil(_viewX / PixelsPerSecond / interval) * interval;
            int count = Mathf.Clamp(Mathf.CeilToInt(_viewWidth / PixelsPerSecond / interval) + 1, 0, 100);
            while (_rulerLabels.Count < count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.top = 30; label.style.fontSize = 10;
                label.style.color = new Color(.55f,.64f,.75f); Add(label); _rulerLabels.Add(label);
            }
            for (int i=0;i<_rulerLabels.Count;i++)
            {
                float time = first + i*interval; var label = _rulerLabels[i];
                label.style.display = i < count && time <= _duration ? DisplayStyle.Flex : DisplayStyle.None;
                label.style.left = TimeToPixel(time); label.text = $"{time:0.##}s";
            }
        }

        private void Draw(MeshGenerationContext context)
        {
            float height = contentRect.height;
            if (!TimelineMath.IsFinite(height) || height <= 0) return;
            float left = Mathf.Max(0, _viewX - 10);
            float right = Mathf.Min(_duration * PixelsPerSecond, _viewX + _viewWidth + 10);
            if (right < left) return;
            var p = context.painter2D;
            DrawGrid(p, height, left, right);
            if (_envelope.Length > 0 && _duration > 0)
            {
                float step = _duration * PixelsPerSecond / _envelope.Length;
                int start = Mathf.Clamp(Mathf.FloorToInt(left / step), 0, _envelope.Length);
                int end = Mathf.Clamp(Mathf.CeilToInt(right / step), 0, _envelope.Length);
                p.strokeColor = new Color(0.2f, 0.6f, 1f, 0.8f);
                p.lineWidth = 1;
                p.BeginPath();
                for (int i = start; i < end; i++)
                {
                    float amplitude = Mathf.Clamp01(_envelope[i]) * Mathf.Max(0, height / 2 - 50);
                    p.MoveTo(new Vector2(i * step, height / 2 - amplitude));
                    p.LineTo(new Vector2(i * step, height / 2 + amplitude));
                }
                p.Stroke();
            }
            for (int i = LowerBound(left / PixelsPerSecond); i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.time * PixelsPerSecond > right) break;
                if (e != _selected) DrawMarker(p, e, false);
            }
            if (_selected != null && _events.Contains(_selected) &&
                _selected.time * PixelsPerSecond >= left && _selected.time * PixelsPerSecond <= right)
                DrawMarker(p, _selected, true);
        }

        private void DrawGrid(Painter2D p, float height, float left, float right)
        {
            double beat = 60.0 / _bpm;
            double grid = beat / _subdivision;
            // 高密度细分隐藏细线，实际吸附精度保持不变。
            int stride = grid * PixelsPerSecond >= 4 ? 1 : _subdivision;
            double spacing = grid * stride;
            if (spacing * PixelsPerSecond < 2) return;
            long first = (long)Math.Ceiling((left / PixelsPerSecond - _offset) / spacing);
            long last = (long)Math.Floor((right / PixelsPerSecond - _offset) / spacing);
            // 防止异常数据造成单次绘制无限循环。
            last = Math.Min(last, first + 20000);
            for (long n = first; n <= last; n++)
            {
                long tick = n * stride;
                bool bar = tick % (_subdivision * _beatsPerMeasure) == 0;
                bool beatLine = tick % _subdivision == 0;
                p.strokeColor = new Color(1, 1, 1, bar ? 0.6f : beatLine ? 0.28f : 0.1f);
                p.lineWidth = bar ? 2 : 1;
                float x = (float)(_offset + n * spacing) * PixelsPerSecond;
                p.BeginPath();
                p.MoveTo(new Vector2(x, 25));
                p.LineTo(new Vector2(x, height));
                p.Stroke();
            }
        }

        private void DrawMarker(Painter2D p, MetroEvent e, bool selected)
        {
            float x = e.time * PixelsPerSecond;
            p.fillColor = selected ? new Color(1, 0.8f, 0) : new Color(0.3f, 0.95f, 0.65f);
            p.BeginPath();
            p.MoveTo(new Vector2(x, MarkerY - 7));
            p.LineTo(new Vector2(x + 6, MarkerY));
            p.LineTo(new Vector2(x, MarkerY + 7));
            p.LineTo(new Vector2(x - 6, MarkerY));
            p.ClosePath();
            p.Fill();
        }
    }
}
