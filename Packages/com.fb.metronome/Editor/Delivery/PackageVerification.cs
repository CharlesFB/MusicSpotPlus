using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace FB.Metronome.Editor.Delivery
{
    public static class PackageVerification
    {
        [MenuItem("Tools/Metronome/Create Sample Scene")]
        public static void CreateSampleScene()
        {
            CreateSampleSceneAsset();
        }
        public static string CreateSampleSceneAsset()
        {
            var type=Type.GetType("FB.Metronome.Samples.MinimalRhythmGame, MetronomeSample");
            if(type==null)throw new InvalidOperationException("Import Minimal Rhythm + DIY from Package Manager first");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var go=new GameObject("Metronome Demo");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent(type);
            var camera=new GameObject("Camera",typeof(Camera),typeof(AudioListener));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera,scene);
            camera.GetComponent<Camera>().backgroundColor=new Color(.055f,.075f,.11f);
            camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
            Directory.CreateDirectory("Assets/MetronomeDemo");
            // === 每次生成新路径，绝不覆盖开发者已编辑的示例场景 ===
            AssetDatabase.Refresh();
            string scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/MetronomeDemo/MinimalRhythm.unity");
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("Could not save sample scene: " + scenePath);
            return scenePath;
        }
        public static void ExportLegacy()
        {
            AssetDatabase.ExportPackage(new[]{"Packages/com.fb.metronome/Runtime", "Packages/com.fb.metronome/Editor", "Packages/com.fb.metronome/Tests"},
                "/tmp/FB.Metronome-0.1.0.unitypackage", ExportPackageOptions.Recurse);
        }
        public static void BuildSample()
        {
            string scenePath = CreateSampleSceneAsset();
            if (scenePath == null) return;
            string path=Environment.GetEnvironmentVariable("METRONOME_BUILD_PATH") ?? "/tmp/MetronomeDemo.app";
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{scenePath},
                locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.Development });
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Sample build failed: "+report.summary.result);
            Debug.Log("[Metronome] Player build succeeded: "+path);
        }
    }
}
