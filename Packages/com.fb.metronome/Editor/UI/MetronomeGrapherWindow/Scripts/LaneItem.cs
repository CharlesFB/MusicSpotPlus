using FB.Metronome.Data;
using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FB.Metronome.Editor.UI
{
    public class LaneItem : VisualElement
    {
        private readonly ObjectField _field;
        private readonly Button _btnRemove;
        private int _index;

        public event Action<int, EventLane> LaneAssigned;
        public event Action<int> LaneRemoved;

        public LaneItem(VisualTreeAsset uxml)
        {
            var root = uxml.CloneTree();
            Add(root);
            style.flexDirection = FlexDirection.Row;
            style.height = 30;
            style.flexGrow = 1;
            style.width = Length.Percent(100);
            root.style.flexGrow = 1;
            root.style.width = Length.Percent(100);

            _field = new ObjectField
            {
                objectType = typeof(EventLane),
                allowSceneObjects = false
            };
            _field.style.flexGrow = 1;
            _field.style.unityTextAlign = TextAnchor.MiddleCenter;
            _field.style.fontSize = 14;
            _field.RegisterValueChangedCallback(OnFieldChanged);

            _btnRemove = root.Q<Button>("btnRemove");
            _btnRemove.clicked += () => LaneRemoved?.Invoke(_index);

            var row = root.Q<VisualElement>("LaneItem");
            row.style.flexGrow = 1;
            row.style.width = Length.Percent(100);
            row.Add(_field);
        }

        public void Bind(EventLane lane, int index)
        {
            _index = index;
            _field.SetValueWithoutNotify(lane);
            bool filled = lane != null;
            _field.SetEnabled(!filled);
            _btnRemove.style.display = filled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            LaneAssigned?.Invoke(_index, evt.newValue as EventLane);
        }
    }
}
