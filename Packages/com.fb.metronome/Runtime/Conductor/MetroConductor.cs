using FB.Metronome.Dispatch;
using FB.Metronome.Data;
using FB.Metronome.Playback;
using UnityEngine.Scripting.APIUpdating;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FB.Metronome.Conductor
{
    /// <summary>
    /// 场景里的运行时入口：绑 AudioSource、MetroSource 和要激活的 EventLane。
    /// 播放 / 时钟 / 分发在 P2、P3 实现。
    /// </summary>
    [MovedFrom(true, sourceNamespace: "MagicTavern.FrontEngine.Metronome", sourceAssembly: "MetronomeRuntime", sourceClassName: "MetroConductor")]
    public class MetroConductor : MonoBehaviour, 
        IAudioClock, 
        IAudioLatency, 
        IMusicPlayer
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private MetroSource source;
        [SerializeField] private List<EventLane> lanes = new List<EventLane>();
        
        public AudioSource AudioSource => audioSource;
        public MetroSource Source => source;
        public IReadOnlyList<EventLane> Lanes => lanes;

        private double _clipOffset; // 记录播放起点，初始为0，Seek后应变更（考虑中途暂停/挪动播放条）
        private double _dspStart; // Dsp时钟标定的起点，用于确定音乐计时的起始位置
        private double _nextClockCheckDsp; // 对表时间节点
        
        // ================ 需要后期根据感官体验调整这些阈值 ================
        [SerializeField, FormerlySerializedAs("ahead")] private float schedulingAhead = .2f;
        public float ahead { get => schedulingAhead; set { CheckFinite(value, nameof(ahead), 0); schedulingAhead=value; } } //留给程序启动准备的播放延迟
        public long latencyTolerance = 200; // 对表时容忍的最大延迟采样数量
        [SerializeField, FormerlySerializedAs("clockCheckInterval")] private double checkInterval = 1;
        public double clockCheckInterval { get => checkInterval; set { CheckFinite(value, nameof(clockCheckInterval), double.Epsilon); checkInterval=value; } } // 对表定时器步长
        // ==============================================================

        #region AudioClock
        
        public double SongTime
        {
            get
            {
                // 未在播 / 未到预约开声点，只返回冻结的谱面位置，禁止用 dspTime 推演
                if (!IsPlaying || DspTime < _dspStart || !audioSource)
                    return _clipOffset;
                // return _clipOffset + (DspTime - _dspStart) * audioSource.pitch;
                return Math.Min(GetClipDuration(), _clipOffset + (DspTime - _dspStart));
            }
        }

        public long SongTimeSamples
        {
            get
            {
                if (!audioSource || !audioSource.clip)
                    return 0;
                if (!IsPlaying || DspTime < _dspStart)
                    return (long)(_clipOffset * audioSource.clip.frequency);
                return (long)(SongTime * audioSource.clip.frequency);
            }
        }

        public double DspTime
        {
            get
            {
                return AudioSettings.dspTime;
            }
        }
        
        #endregion AudioClock
        
        #region MusicPlayer
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 播放，含暂停后恢复播放的语义
        /// </summary>
        public void Play() 
        {
            ValidateParameters();
            if (!isActiveAndEnabled) return;
            if (!audioSource || !source || !source.clip)
            {
                Debug.LogWarning("[Metronome] Conductor 检测到脚本存在未装载资源，请仔细检查 Inspector 中的配置。");
                return;
            }
            
            // 忽略重复播放
            if (IsPlaying)
                return;

            if (State == PlaybackState.Ended) Stop();
            EnsureLaneTrackCount();
            controlVersion++; State = PlaybackState.Scheduled;
            HaltAudio(); // 停止播放器
            IsPlaying = true;
            _dspStart = DspTime + Math.Max(0, ahead);
            _nextClockCheckDsp = _dspStart + clockCheckInterval;
            EnsureLaneTrackCount();
            
            audioSource.playOnAwake = false;
            audioSource.pitch = 1; audioSource.loop = false;
            audioSource.clip = source.clip;
            audioSource.timeSamples = Math.Min(audioSource.clip.samples - 1, (int)(_clipOffset * audioSource.clip.frequency));
            audioSource.PlayScheduled(_dspStart);
        }

        public void Stop()
        {
            controlVersion++; dispatcher.Reset(); lookAheadDispatcher.Reset();
            State = PlaybackState.Stopped;
            _clipOffset = 0;
            IsPlaying = false;
            HaltAudio();
        }

        public void Pause()
        {
            if (!IsPlaying)
                return;

            controlVersion++; dispatcher.Invalidate(); lookAheadDispatcher.Invalidate();
            State = PlaybackState.Paused;
            _clipOffset = SongTime; // 记录暂停时的offset
            IsPlaying = false; // 标记未在播，必须在 Halt 前取 SongTime
            HaltAudio();
        }

        public void Seek(float timeSeconds)
        {
            ValidateParameters();
            if (!MetroValidation.IsFinite(timeSeconds)) throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            if (!audioSource)
                return;
            
            //Seek 钳制到 clip 长度
            float duration = GetClipDuration();
            timeSeconds = Mathf.Clamp(timeSeconds, 0f, Mathf.Max(0, duration));
            if (State == PlaybackState.Ended) State = PlaybackState.Stopped;

            controlVersion++; dispatcher.Seek(timeSeconds); lookAheadDispatcher.Seek(timeSeconds);

            _clipOffset = timeSeconds;
            HaltAudio();
            if (source != null && source.clip != null)
                audioSource.clip = source.clip;
            if (audioSource.clip) audioSource.timeSamples = Math.Min(audioSource.clip.samples - 1, (int)(timeSeconds * audioSource.clip.frequency));

            if (IsPlaying)
            {
                State = PlaybackState.Scheduled;
                _dspStart = DspTime + Math.Max(0, ahead);
                _nextClockCheckDsp = _dspStart + clockCheckInterval;
                audioSource.PlayScheduled(_dspStart);
            }
        }
        
        #endregion MusicPlayer
        
        #region AudioLatency
        [SerializeField, FormerlySerializedAs("calibrationMs")] private double calibration;
        public double calibrationMs { get => calibration; set { CheckFinite(value, nameof(calibrationMs)); calibration=value; } } // 外部延迟，后续手动调整
        public AudioLatency GetLatency()
        {
            CheckFinite(calibrationMs,nameof(calibrationMs));
            AudioSettings.GetDSPBufferSize(out var length, out var count);
            int outputRate = AudioSettings.outputSampleRate;
            double dspBufferLatency = outputRate > 0
                ? (double)1000 * length * count / outputRate
                : 0.0;
            AudioLatency latency = new(dspBufferLatency, calibrationMs);
            return latency;
        }
        
        #endregion AudioLatency

        private void OnDisable() { Stop(); }
        private void OnDestroy() { Stop(); }
        private void Update()
        {
            if (!IsPlaying || DspTime < _dspStart) return;
            State = PlaybackState.Playing;
            if (!source || !audioSource || audioSource.clip != source.clip || audioSource.pitch != 1 || audioSource.loop)
            { Stop(); return; }
            int stamp = controlVersion;
            if (MetroValidation.IsFinite(LookAheadSeconds))
                lookAheadDispatcher.Tick(SongTime + Math.Max(0, LookAheadSeconds), emitApproaching ??= EmitApproaching, LogDispatchError);
            if (stamp != controlVersion) return;
            ActivateOnLaneEvent();
            if (stamp != controlVersion) return;
            if (SongTime >= GetClipDuration())
            {
                _clipOffset = GetClipDuration(); IsPlaying = false;
                State = PlaybackState.Ended; HaltAudio(); return;
            }
            if (!audioSource.isPlaying) { Pause(); return; }
            if (DspTime >= _nextClockCheckDsp)
            { ClockCheck(); _nextClockCheckDsp = DspTime + Math.Max(.05, clockCheckInterval); }
        }

        private void OnValidate()
        {
            if (audioSource)
                audioSource.playOnAwake = false;

            if (!source || lanes == null)
            {
                return;
            }

            for (int i = 0; i < lanes.Count; i++)
            {
                EventLane lane = lanes[i];
                if (lane && lane.source && !Equals(lane.source, source))
                {
                    Debug.LogWarning(
                        $"[Metronome] EventLane '{lane.name}' references a different MetroSource than this Conductor.",
                        this);
                }
            }
        }

        /// <summary>
        /// 对表调整播放跟踪延迟
        /// </summary>
        public void ClockCheck()
        {
            // 未开声 / 未在播 / 无 clip 时 timeSamples 不可信，禁止对表
            if (!IsPlaying || DspTime < _dspStart || !audioSource || !audioSource.clip || !audioSource.isPlaying)
                return;

            // === timeSamples 可能以音频缓冲块跳变，仅作为诊断，不污染平滑 DSP 时钟 ===
            ClockDriftSamples = audioSource.timeSamples - SongTimeSamples;
        }

        /// <summary>
        /// 只停 audioSource 出声音
        /// </summary>
        private void HaltAudio()
        {
            if (audioSource)
                audioSource.Stop();
        }

        private float GetClipDuration()
        {
            if (source) return source.clip ? source.clip.length : source.duration;
            return audioSource && audioSource.clip ? audioSource.clip.length : 0f;
        }

        #region SubscribeDelegate

        public delegate void MetroEventHandler(EventLane lane, MetroEvent e);
        
        private Dictionary<string, MetroEventHandler> _byEventId = new Dictionary<string, MetroEventHandler>();
        private Dictionary<string, MetroEventHandler> _byLane = new Dictionary<string, MetroEventHandler>();
        private Dictionary<(string laneId, string eventId), MetroEventHandler> _byLaneEvent = new Dictionary<(string laneId, string eventId), MetroEventHandler>();

        private readonly EventDispatcher dispatcher = new EventDispatcher();
        private readonly EventDispatcher lookAheadDispatcher = new EventDispatcher();
        /// <summary>预告窗口，谱面秒；不改变到点订阅语义。默认 2 秒。</summary>
        private float lookAheadSeconds = 2;
        public float LookAheadSeconds { get => lookAheadSeconds; set { CheckFinite(value, nameof(LookAheadSeconds), 0); lookAheadSeconds=value; } }
        // === 所有调度参数先验证，拒绝 NaN/Infinity；序列化字段同样在入口验证 ===
        private static void CheckFinite(double value, string name, double minimum=double.NegativeInfinity)
        { if(!MetroValidation.IsFinite(value) || value<minimum) throw new ArgumentOutOfRangeException(name); }
        private void ValidateParameters()
        {
            CheckFinite(ahead,nameof(ahead),0);
            CheckFinite(clockCheckInterval,nameof(clockCheckInterval),double.Epsilon);
            CheckFinite(calibrationMs,nameof(calibrationMs));
            CheckFinite(LookAheadSeconds,nameof(LookAheadSeconds),0);
        }
        public event MetroEventHandler EventApproaching;
        private readonly Dictionary<EventLane, string> laneIds = new Dictionary<EventLane, string>();
        private bool snapshotReady;
        private Action<EventLane, MetroEvent> emitDue, emitApproaching;
        private static readonly Action<Exception> LogDispatchError = ex => Debug.LogException(ex);
        private int controlVersion;
        public enum PlaybackState { Stopped, Scheduled, Playing, Paused, Ended }
        public PlaybackState State { get; private set; }
        public long ClockDriftSamples { get; private set; }

        // === 所有装载入口先验证；失败不破坏正在使用的配置 ===
        public void Configure(AudioSource output, MetroSource music, IEnumerable<EventLane> activeLanes)
        {
            ValidateParameters();
            if (!output) throw new ArgumentNullException(nameof(output));
            var copy = new List<EventLane>(activeLanes ?? throw new ArgumentNullException(nameof(activeLanes)));
            MetroValidation.Validate(music, copy);
            Stop(); audioSource = output; source = music; lanes = copy;
            RefreshEvents();
        }
        public void RefreshEvents()
        {
            ValidateParameters();
            MetroValidation.Validate(source, lanes);
            dispatcher.Load(lanes); lookAheadDispatcher.Load(lanes);
            laneIds.Clear(); foreach (var lane in lanes) laneIds[lane] = lane.laneId;
            if (State != PlaybackState.Stopped || _clipOffset > 0) { dispatcher.Seek(SongTime); lookAheadDispatcher.Seek(SongTime); }
            snapshotReady = true; controlVersion++;
        }
        private void EnsureLaneTrackCount() { if (!snapshotReady) RefreshEvents(); }
        private void EmitApproaching(EventLane lane, MetroEvent e) => InvokeCopied(EventApproaching, lane, e);
        private void ActivateOnLaneEvent() => dispatcher.Tick(SongTime, emitDue ??= InvokeLaneEvent, LogDispatchError);
        private void InvokeLaneEvent(EventLane lane, MetroEvent e)
        {
            _byEventId.TryGetValue(e.eventId, out var byEvent);
            string laneId = laneIds.TryGetValue(lane, out var storedId) ? storedId : lane.laneId;
            _byLane.TryGetValue(laneId, out var byLane);
            _byLaneEvent.TryGetValue((laneId, e.eventId), out var byLaneEvent);
            InvokeCopied(byEvent + byLane + byLaneEvent, lane, e);
        }
        private static void InvokeCopied(MetroEventHandler handler, EventLane lane, MetroEvent e)
        {
            if (handler == null) return;
            // 同一事件的订阅者全部通知；异常隔离，后续事件由控制版本决定是否继续。
            foreach (MetroEventHandler callback in handler.GetInvocationList())
                try { callback(lane, e); } catch (Exception ex) { Debug.LogException(ex); }
        }

        public void Subscribe(string eventId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(eventId) || handler == null) return;
            if (_byEventId.TryGetValue(eventId, out var existing))
                _byEventId[eventId] = existing + handler;
            else
                _byEventId[eventId] = handler;
        }

        public void Unsubscribe(string eventId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(eventId) || handler == null) return;
            if (!_byEventId.TryGetValue(eventId, out var existing)) return;
            existing -= handler;
            if (existing == null) _byEventId.Remove(eventId);
            else _byEventId[eventId] = existing;
        }

        public void Subscribe(string laneId, string eventId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(laneId) || string.IsNullOrEmpty(eventId) || handler == null) return;
            if (_byLaneEvent.TryGetValue((laneId, eventId), out var existing))
                _byLaneEvent[(laneId, eventId)] = existing + handler;
            else 
                _byLaneEvent[(laneId, eventId)] = handler;
        }

        public void Unsubscribe(string laneId, string eventId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(laneId) || string.IsNullOrEmpty(eventId) || handler == null) return;
            if (!_byLaneEvent.TryGetValue((laneId, eventId), out var existing)) return;
            existing -= handler;
            if (existing == null) _byLaneEvent.Remove((laneId, eventId));
            else _byLaneEvent[(laneId, eventId)] = existing;
        }

        public void SubscribeLane(string laneId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(laneId) || handler == null) return;
            if (_byLane.TryGetValue(laneId, out var existing))
                _byLane[laneId] = existing + handler;
            else
                _byLane[laneId] = handler;
        }

        public void UnsubscribeLane(string laneId, MetroEventHandler handler)
        {
            if (string.IsNullOrEmpty(laneId) || handler == null) return;
            if (!_byLane.TryGetValue(laneId, out var existing)) return;
            existing -= handler;
            if (existing == null) _byLane.Remove(laneId);
            else _byLane[laneId] = existing;
        }

        #endregion SubscribeDelegate
    }
}
