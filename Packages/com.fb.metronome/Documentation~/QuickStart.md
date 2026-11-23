# 快速开始与行为约定

## 编谱与运行

1. Create → Metronome → Metro Source，放入 AudioClip。
2. Create → Metronome → Event Lane，填入唯一 laneId，并绑定上一步 Source。
3. 从 Source/Lane Inspector 打开窗口，拖入 Lane。设置 BPM、拍号、每拍细分及吸附。
4. 点击波形设置 Position，Add event；选中 marker / 卡片修改 ID、时间、Payload 后 Apply。
5. marker 可拖动，支持 Undo/Redo；窗口内非文本输入焦点下，Delete 删除、Ctrl/⌘ C/V 复制到当前 Position。
6. 场景中添加 AudioSource 和 MetroConductor，配置资源或使用代码：

```csharp
using FB.Metronome.Conductor;
using FB.Metronome.Data;

conductor.Configure(audioSource, source, new[] { notes, effects });
conductor.Subscribe("tap", OnTap);                    // 所有激活 Lane
conductor.Subscribe("notes", "tap", OnNote);         // 一条 Lane + ID
conductor.SubscribeLane("effects", OnEffect);         // 一条 Lane
conductor.Play();
// 生命周期结束时使用相同方法引用对称 Unsubscribe / UnsubscribeLane。
```

handler 类型是 `MetroConductor.MetroEventHandler(EventLane lane, MetroEvent e)`。
配置的 Lane 列表就是激活的 Lane。所有 Lane 必须指向同一 Source，laneId 不得重复。

## 时间、分发和快照

- 时间单位为秒。BPM 每拍对应拍号分母音符；6/8 表示六个八分音符拍，不自动推导复合拍。
- 修改 BPM 不移动已有事件。吸附仅作用于新增、移动、修改时间。
- 正常扫描包含右端点；首次从零播放包含零秒事件，片尾事件先分发再进入 Ended。
- Seek 跳过目标及以前的事件，不补发跨越部分；向后 Seek 允许后续重新触发。
- Pause 保留扫描进度；Stop 清零。Scheduled 表示预约等待，不在预约前分发。
- 顺序：按事件秒数排序；相同时间按 Configure 的 Lane 顺序，再按 Lane 列表原始顺序。
- Configure / RefreshEvents 验证并复制事件数据。播放中直接改资产不会改变快照，需要显式 RefreshEvents；刷新跳过当前时间及以前事件。
- 回调异常被记录，其他订阅者继续收到同一事件；回调中的 Pause/Stop/Seek 使后续旧扫描失效。
- pitch 固定为 1，loop 关闭。播放期间外部修改 clip/pitch/loop 会停止 Conductor；直接停止 AudioSource 会暂停 Conductor。不要绕开入口控制音频。
- Disable/Destroy 停止声音并清零。Configure 会停止旧音乐，订阅仍保留，换曲时由游戏决定是否取消旧订阅。
- EventApproaching 是独立预告通道，`LookAheadSeconds` 为谱面秒。Seek 后重新预告目标以后的事件；不要把预告当成到点事件。
- 回调在 Unity 主线程帧更新中发生，不是音频线程采样级回调。音频精确调度应使用 DSP / PlayScheduled。

## 延迟

`SongTime` 是未补偿的谱面时钟。`ClockDriftSamples` 仅报告 timeSamples 与 DSP 推算的差异，不将缓冲台阶写回时钟。

`OutputMs` 是 DSP 缓冲估算，不是端到端硬件测量。`calibrationMs` 是估算之外的额外残差；正值表示听感/输入更晚。`TotalMs = OutputMs + CalibrationMs`。
示例用 `GetLatency().ToAudibleSongTime(SongTime)` 判定，相当于减掉总延迟一次。
外层若测到的是总偏移，使用 `AudioLatency.FromMeasuredTotal` 换算残差，避免重复扣减。校准 UI、用户设置保存与测量流程归游戏/模板负责。

## DIY 与本地音频

`ChartSession.Clone(source, lanes)` 创建独立的 Source/Lane/Event，编辑副本不会改开发者资源。`ChartSession.ToJson` / `FromJson` 使用版本 1 的 DTO；不存委托或场景对象。
`stableId` 是事件持久身份，`index` 仅是排序后的显示编号。新增事件生成新 ID，排序保持 ID。

```csharp
using var session = ChartSession.Clone(source, lanes);
var note = session.Lanes[0].AddEvent(1.25f, "tap", snap: true);
string json = ChartSession.ToJson(session.Source, session.Lanes);
// 由游戏负责写入 persistentDataPath 或其他存储。
```

本地音频使用新的 `LocalAudioLoader`，主线程 `yield return loader.Load(absolutePath, cancellationToken)`；检查 State / Error。
支持 wav/ogg/mp3，实际解码依平台；不支持网络 URL、Addressables 或任意文件类型。
已有 AudioClip 可直接传入 Configure 的 Source；加载器也可包装既有 clip，此时不取得释放所有权。
加载器 State 独立于播放器的 Stopped/Scheduled/Playing/Paused/Ended。加载完成再 Configure，可在新歌加载过程中继续旧歌。
Dispose 取消正在加载的请求，并释放它拥有的 clip。不要在 Conductor 仍使用该 clip 时 Dispose；先停止或切换曲目。
ChartSession.Dispose 释放自己的 ScriptableObjects，但不释放调用方提供的 clip。

## Editor 试听与限制

窗口使用 Unity 内部 AudioUtil；不可用时编谱仍能继续。全局试听通道可能被 Inspector 抢占；检测位置不连续后放弃所有权，避免停止其他窗口的新试听。
Unity 2022 未提供可查询的当前试听 clip 身份，因此接近相同进度的外部抢占无法可靠识别。建议编谱试听时不要同时在 Inspector 试听另一首歌。
BPM 是建议值，复杂音乐存在半拍/双拍歧义，应人工确认；静音、太短和读取失败不会覆盖已填写 BPM。

## 测试与兼容

最低声明版本 Unity 2022.3。Test Runner 使用 `com.unity.test-framework`；在项目 manifest 的 `testables` 加入 `com.fb.metronome`。
Editor tests 在 EditMode；Runtime tests 在 PlayMode。测试使用生成的 PCM，不依赖示例音频路径。
新版本保留原程序集名称和脚本 GUID，并通过 MovedFrom 迁移旧类型命名空间。
