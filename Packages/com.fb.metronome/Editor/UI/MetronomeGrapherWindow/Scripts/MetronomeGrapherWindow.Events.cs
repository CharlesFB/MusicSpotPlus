using FB.Metronome.Data;
using UnityEditor;
using UnityEngine;
namespace FB.Metronome.Editor.UI
{
    public partial class MetronomeGrapherWindow
    {
        private void RefreshEvents()
        {
            if (!_guiReady) return;
            var events = _currentLane != null ? _currentLane.events : null;
            if (events == null || !events.Contains(_selectedEvent)) _selectedEvent = null;
            _selectedIndex = _selectedEvent != null ? events.IndexOf(_selectedEvent) : -1;
            _eventStrip.Bind(events, _selectedEvent);
            _track.SetEvents(events, _selectedEvent);
            RefreshEventInfo();
            _btnAddEvent.SetEnabled(_currentLane != null && Duration > 0);
        }

        private void SelectEvent(MetroEvent e, bool revealCard)
        {
            _selectedEvent = e;
            _selectedIndex = _currentLane != null && e != null ? _currentLane.events.IndexOf(e) : -1;
            _eventStrip.Select(e, revealCard);
            _track.SetSelected(e);
            RefreshEventInfo();
            if (!revealCard && e != null)
            {
                float x = _track.TimeToPixel(e.time);
                float width = _spectrumScroll.contentViewport.layout.width;
                if (x < _spectrumScroll.scrollOffset.x || x > _spectrumScroll.scrollOffset.x + width)
                    _spectrumScroll.scrollOffset = new Vector2(Mathf.Max(0, x - width / 2), 0);
            }
        }

        private float EventTime(float time) => TimelineMath.EventTime(time, Duration, _source,
            _currentLane != null ? Mathf.Clamp(_currentLane.snapSubdivision, 1, 64) : 4, _snapEnabled);

        private void AddEvent()
        {
            if (_currentLane == null || Duration <= 0 || !CommitSeekField()) return;
            string id = string.IsNullOrWhiteSpace(_currentLane.laneId) ? "event" : _currentLane.laneId;
            Undo.RecordObject(_currentLane, "Add MetroEvent");
            _selectedEvent = _currentLane.AddEvent(EventTime((float)_seekSeconds), id, snap: false);
            EditorUtility.SetDirty(_currentLane);
            RefreshEvents();
        }

        private void DeleteEvent()
        {
            if (_currentLane == null || _selectedEvent == null) return;
            Undo.RecordObject(_currentLane, "Delete MetroEvent");
            if (_currentLane.RemoveEvent(_selectedEvent)) EditorUtility.SetDirty(_currentLane);
            _selectedEvent = null;
            RefreshEvents();
        }

        private void RefreshEventInfo()
        {
            bool selected = _selectedEvent != null && _currentLane != null;
            _tfIndex.SetValueWithoutNotify(selected ? _selectedEvent.index.ToString() : "");
            _tfEventId.SetValueWithoutNotify(selected ? _selectedEvent.eventId ?? "" : "");
            // round-trip 格式避免只改 payload 时把原有时间截成毫秒精度。
            _tfTime.SetValueWithoutNotify(selected ? Number(_selectedEvent.time) : "");
            _tfPayload.SetValueWithoutNotify(selected ? _selectedEvent.payload ?? "" : "");
            _tfEventId.SetEnabled(selected);
            _tfTime.SetEnabled(selected);
            _tfPayload.SetEnabled(selected);
            _btnDeleteEvent.SetEnabled(selected);
            _btnApplyChange.SetEnabled(false);
        }

        private void RefreshApplyState()
        {
            bool dirty = _selectedEvent != null && _currentLane != null &&
                (_tfEventId.value != (_selectedEvent.eventId ?? "") || _tfTime.value != Number(_selectedEvent.time) ||
                 _tfPayload.value != (_selectedEvent.payload ?? ""));
            _btnApplyChange.SetEnabled(dirty);
        }

        private void ApplyEventChange()
        {
            if (_currentLane == null || _selectedEvent == null) return;
            if (!TimelineMath.TrySeconds(_tfTime.value, out float time) || string.IsNullOrWhiteSpace(_tfEventId.value))
            {
                ShowStatus("事件 ID 不能为空；Time 必须是有限秒数。本次修改尚未提交。", true);
                return;
            }
            Undo.RecordObject(_currentLane, "Edit MetroEvent");
            // 只改 ID/payload 时不重新吸附原本自由放置的事件。
            if (time != _selectedEvent.time) _selectedEvent.time = EventTime(time);
            _selectedEvent.eventId = _tfEventId.value.Trim();
            _selectedEvent.payload = _tfPayload.value;
            _currentLane.Renumber();
            EditorUtility.SetDirty(_currentLane);
            RefreshEvents();
        }

    }
}
