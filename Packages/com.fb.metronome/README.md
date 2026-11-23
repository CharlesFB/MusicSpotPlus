# FB Metronome

Unity 2022.3 音乐时钟、事件轨、编谱窗口和玩家 DIY 数据接口。

安装：Package Manager → Add package from disk → 选择本目录的 `package.json`。
也可将本目录作为 embedded package 放进项目的 `Packages/com.fb.metronome`。

快速开始见 [Documentation~/QuickStart.md](Documentation~/QuickStart.md)。
Samples → **Minimal Rhythm + DIY** → Import。之后选择 **Tools → Metronome → Create Sample Scene**，打开 `Assets/MetronomeDemo/MinimalRhythm.unity` 并 Play。
示例自带程序生成的原创 120 BPM 音乐，不依赖工程原有的 MusicTest.wav。

核心没有 UniTask 或 Addressables 依赖。支持平台的音频解码能力仍由 Unity 决定。
