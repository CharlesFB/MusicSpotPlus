using FB.Metronome.Data;
using FB.Metronome.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace FB.Metronome.Editor.Inspectors
{
    [CustomEditor(typeof(MetroSource))]
    public class MetroSourceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var source = (MetroSource)target;
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(source == null))
            {
                if (GUILayout.Button("Open Metronome Grapher"))
                {
                    MetronomeGrapherWindow.ShowMetronomeTimeline(source);
                }
            }
        }
    }
}
