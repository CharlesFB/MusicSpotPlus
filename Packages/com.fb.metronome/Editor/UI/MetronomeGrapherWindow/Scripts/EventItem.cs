using FB.Metronome.Data;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FB.Metronome.Editor.UI
{
    public class EventItem : VisualElement
    {
        private static readonly Color BorderIdle = new Color(.22f,.30f,.40f);
        private static readonly Color BorderSelected = new Color(1f, 0.8f, 0f);
        private static readonly Color FillIdle = new Color(.12f,.19f,.27f);
        private static readonly Color FillSelected = new Color(0x6a / 255f, 0x5a / 255f, 0x18 / 255f);

        private readonly VisualElement _card;
        private readonly Label _lbIndex;

        public MetroEvent Event { get; private set; }

        public event Action<EventItem> Clicked;

        public EventItem(VisualTreeAsset uxml)
        {
            var root = uxml.CloneTree();
            Add(root);
            style.flexGrow = 0;
            style.flexShrink = 0;
            style.width = 90;
            style.minWidth = 90;
            style.height = 30;

            root.style.flexGrow = 1;
            root.style.height = 30;
            root.pickingMode = PickingMode.Ignore;

            _card = root.Q<VisualElement>("EventItem");
            _card.style.flexGrow = 1;
            _card.style.height = 30;
            _card.pickingMode = PickingMode.Position;

            _lbIndex = root.Q<Label>("lbIndex");
            _lbIndex.pickingMode = PickingMode.Ignore;

            pickingMode = PickingMode.Position;
            RegisterCallback<ClickEvent>(OnClicked, TrickleDown.TrickleDown);
            ApplySelected(false);
        }

        public void Bind(MetroEvent e)
        {
            Event = e;
            _lbIndex.text = e != null ? $"{e.index}: {e.eventId}" : "-";
            tooltip = e != null ? $"{e.eventId}  {e.time:0.###}s" : "";
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("event-item--selected", selected);
            _card.EnableInClassList("event-item--selected", selected);
            ApplySelected(selected);
        }

        private void ApplySelected(bool selected)
        {
            Color border = selected ? BorderSelected : BorderIdle;
            _card.style.borderTopColor = border;
            _card.style.borderRightColor = border;
            _card.style.borderBottomColor = border;
            _card.style.borderLeftColor = border;
            _card.style.backgroundColor = selected ? FillSelected : FillIdle;
        }

        private void OnClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            Clicked?.Invoke(this);
        }
    }
}
