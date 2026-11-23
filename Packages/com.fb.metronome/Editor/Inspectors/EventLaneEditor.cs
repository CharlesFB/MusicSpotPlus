using FB.Metronome.Data;
using FB.Metronome.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace FB.Metronome.Editor.Inspectors
{
    [CustomEditor(typeof(EventLane))]
    public class EventLaneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var lane = (EventLane)target;
            using (new EditorGUI.DisabledScope(lane.source == null))
            {
                if (GUILayout.Button("Open Metronome Grapher"))
                    MetronomeGrapherWindow.ShowLane(lane);
            }
            if (lane.source == null)
            {
                EditorGUILayout.HelpBox("Assign a MetroSource. The lane timeline is taken from that source.", MessageType.Warning);
            }
        }
    }
}
