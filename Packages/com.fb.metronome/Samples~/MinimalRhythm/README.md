# Minimal Rhythm + DIY

Import this sample, choose Tools → Metronome → Create Sample Scene, open the generated scene, then press Play.

- Play / Pause / Stop / position slider controls the Conductor.
- Space hits approaching notes, ±150 ms. A second event lane drives the hit-line pulse.
- DIY mode adds snapped notes, removes the last note, saves/restores JSON under Application.persistentDataPath.
- Enter an absolute wav/ogg/mp3 path to replace audio; Cancel aborts loading.
- Calibration slider stores extra residual milliseconds in PlayerPrefs. It is a demonstration UI, not a hardware measurement tool.
- The 24-second, 120 BPM song is generated at runtime. You may instead assign an AudioClip in the Inspector.

Core code has no dependency on this sample. It uses IMGUI to keep the example free of UI prefab and asset dependencies.
