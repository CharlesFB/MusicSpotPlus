using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FB.Metronome.Conductor;
using FB.Metronome.Data;
using FB.Metronome.Loading;
using FB.Metronome.Serialization;
using UnityEngine;
namespace FB.Metronome.Samples
{
    /// <summary>将脚本挂到空物体即可运行。玩法、DIY UI 和用户设置属于示例，不属于核心包。</summary>
    public sealed class MinimalRhythmGame : MonoBehaviour
    {
        public AudioClip music;
        private MetroConductor conductor;
        private ChartSession session;
        private AudioClip defaultMusic;
        private string defaultHash;
        private ChartDocument pendingDocument;
        private readonly DemoJudgementClock judgementClock=new DemoJudgementClock();
        private double judgementTime;
        private bool tailPaused;
        private EventLane Notes => session.Lanes.Find(lane => lane.laneId == "notes");
        private LocalAudioLoader loader, loadedAudio;
        private readonly List<MetroEvent> visible = new List<MetroEvent>();
        private readonly HashSet<string> judged = new HashSet<string>();
        private int score, misses, pulseCount;
        private float flash;
        private bool diy, generated;
        private string message="Space 命中音符；Pause 暂停。", path="";
        private string SavePath => Path.Combine(Application.persistentDataPath,"MetronomeDemo.json");
        private void Start()
        {
            if(!music){music=CreateSong();generated=true;}
            defaultMusic=music;defaultHash=DemoChartStore.HashClip(music);
            var source=ScriptableObject.CreateInstance<MetroSource>();source.clip=music;source.duration=music.length;source.bpm=120;source.audioHash=defaultHash;source.audioId="pcm:"+defaultHash;
            var notes=ScriptableObject.CreateInstance<EventLane>();notes.source=source;notes.laneId="notes";
            var effects=ScriptableObject.CreateInstance<EventLane>();effects.source=source;effects.laneId="effects";
            for(float t=2;t<music.length-.5f;t+=.5f)notes.AddEvent(t,"tap",false);
            for(float t=2;t<music.length;t+=2)effects.AddEvent(t,"pulse",false);
            session=ChartSession.Clone(source,new[]{notes,effects});
            Destroy(source);Destroy(notes);Destroy(effects);
            var audio=gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;
            conductor=gameObject.AddComponent<MetroConductor>();
            conductor.Configure(audio,session.Source,session.Lanes);
            var savedCalibration=PlayerPrefs.GetFloat("MetronomeDemo.Calibration",0);
            conductor.calibrationMs=MetroValidation.IsFinite(savedCalibration)?savedCalibration:0;
            conductor.EventApproaching+=OnApproaching;
            conductor.Subscribe("effects","pulse",OnPulse);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-metronome-smoke") >= 0) StartCoroutine(Smoke());
        }
        private void OnApproaching(EventLane lane,MetroEvent e)
        { if(lane.laneId=="notes" && !judged.Contains(e.stableId) && !visible.Exists(n=>n.stableId==e.stableId)) visible.Add(e); }
        private void OnPulse(EventLane lane,MetroEvent e) { flash=1; pulseCount++; }
        private void Update()
        {
            if(!conductor)return;
            flash=Mathf.Max(0,flash-Time.deltaTime*3);
            if(!judgementClock.TryTime(conductor.State,conductor.SongTime,conductor.DspTime,music.length,
                conductor.GetLatency().TotalMs/1000,out double now))return;
            judgementTime=now;
            if(Input.GetKeyDown(KeyCode.Space) && !diy)
            {
                MetroEvent nearest=null;double distance=.15;
                foreach(var e in visible)if(!judged.Contains(e.stableId) && Math.Abs(e.time-now)<distance)
                { nearest=e;distance=Math.Abs(e.time-now); }
                if(nearest!=null){judged.Add(nearest.stableId);score++;message=$"Hit · {distance*1000:0} ms";}
                else message="No note in the ±150 ms window";
            }
            for(int i=visible.Count-1;i>=0;i--)
            {
                var e=visible[i];
                if(judged.Contains(e.stableId)){visible.RemoveAt(i);continue;}
                if(e.time < now-.15){judged.Add(e.stableId);visible.RemoveAt(i);if(!diy)misses++;}
            }
        }
        private void Seek(float time)
        {
            judgementClock.Reset();tailPaused=false;
            conductor.Seek(time);visible.Clear();judged.Clear();score=misses=0;
            // Seek 目标及以前视为已过，未来预告由独立 LookAhead 游标重建。
            foreach(var lane in session.Lanes)foreach(var e in lane.events)if(e.time<=time)judged.Add(e.stableId);
        }
        private void OnGUI()
        {
            if(!conductor)return;
            GUI.backgroundColor=new Color(.15f,.28f,.35f);
            GUILayout.BeginArea(new Rect(24,24,Mathf.Min(Screen.width-48,850),300),GUI.skin.box);
            GUILayout.Label("METRONOME / PLAY + DIY");
            GUILayout.Label($"{conductor.State}   {conductor.SongTime:0.00}s / {music.length:0.00}s   Score {score}   Miss {misses}");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Play / Resume"))
            {
                if(tailPaused){judgementClock.ResumeTail(conductor.DspTime);tailPaused=false;}
                else {if(conductor.State==MetroConductor.PlaybackState.Ended){conductor.Stop();Seek(0);}conductor.Play();}
            }
            if(GUILayout.Button("Pause"))
            {
                if(conductor.State==MetroConductor.PlaybackState.Ended){judgementClock.PauseTail(conductor.DspTime);tailPaused=true;}
                else {conductor.Pause();judgementClock.Reset();}
            }
            if(GUILayout.Button("Stop"))StopPlayback();
            diy=GUILayout.Toggle(diy,"DIY mode");GUILayout.EndHorizontal();
            float seek=GUILayout.HorizontalSlider((float)conductor.SongTime,0,music.length);
            if(Mathf.Abs(seek-(float)conductor.SongTime)>.02f)Seek(seek);
            double calibration=GUILayout.HorizontalSlider((float)conductor.calibrationMs,-300,300);
            if(Math.Abs(calibration-conductor.calibrationMs)>.1){conductor.calibrationMs=calibration;PlayerPrefs.SetFloat("MetronomeDemo.Calibration",(float)calibration);}
            GUILayout.Label($"Extra calibration {conductor.calibrationMs:0} ms | buffer estimate {conductor.GetLatency().OutputMs:0} ms");
            if(diy)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Add snapped note"))
                {
                    Notes.AddEvent((float)conductor.SongTime,"tap");conductor.RefreshEvents();message="Note added to runtime copy";
                }
                if(GUILayout.Button("Remove last note") && Notes.events.Count>0)
                {
                    var lane=Notes;lane.RemoveEvent(lane.events[lane.events.Count-1]);conductor.RefreshEvents();visible.Clear();
                }
                if(GUILayout.Button("Save JSON"))Try(()=>{DemoChartStore.Save(SavePath,ChartSession.ToJson(session.Source,session.Lanes));message=SavePath;});
                if(GUILayout.Button("Load JSON"))Try(()=>{pendingDocument=DemoChartStore.Read(SavePath,out bool recovered);
                    message=recovered?"Backup recovered":"Chart read";StartCoroutine(RestoreAudio(pendingDocument));});
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();path=GUILayout.TextField(path);
                if(GUILayout.Button("Load local audio",GUILayout.Width(120)))StartCoroutine(LoadAudio());
                if(GUILayout.Button("Cancel",GUILayout.Width(65))){loader?.Dispose();loader=null;message="Canceled";}
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if(pendingDocument!=null && GUILayout.Button("Relocate saved music to path"))
                {pendingDocument.audioPath=path;StartCoroutine(RestoreAudio(pendingDocument));}
                if(pendingDocument!=null && string.IsNullOrEmpty(pendingDocument.audioHash) && GUILayout.Button("Confirm music for legacy chart"))
                {pendingDocument.audioPath=path;StartCoroutine(RestoreAudio(pendingDocument,true));}
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(message);GUILayout.EndArea();
            float hitX=Screen.width*.25f,y=Screen.height*.67f,w=Screen.width*.65f;
            GUI.color=new Color(.2f+flash*.3f,.8f, .7f);GUI.DrawTexture(new Rect(hitX,y-25,3,80),Texture2D.whiteTexture);
            double clock=judgementTime;
            foreach(var e in visible)
            {
                float x=hitX+(float)(e.time-clock)/Math.Max(.001f,conductor.LookAheadSeconds)*w;
                if(x>=0 && x<Screen.width)GUI.DrawTexture(new Rect(x-9,y,18,28),Texture2D.whiteTexture);
            }
            GUI.color=Color.white;GUI.Label(new Rect(hitX-35,y+60,200,25),"SPACE / ±150 ms");
        }
        private IEnumerator LoadAudio()
        {
            // === 候选副本验证成功前，不修改正在使用的音乐和事件 ===
            ChartDocument document=null;
            Try(()=>document=ChartSession.ReadDocument(ChartSession.ToJson(session.Source,session.Lanes)));
            if(document==null)yield break;
            if(string.IsNullOrWhiteSpace(path)){message="Enter an absolute audio path";yield break;}
            document.audioPath=path;
            yield return RestoreAudio(document,true);
        }
        private IEnumerator RestoreAudio(ChartDocument document, bool acceptNewMusic=false)
        {
            loader?.Dispose();loader=null;
            // Snapshot the request so UI relocation cannot change an in-flight document.
            try {document=ChartSession.ReadDocument(JsonUtility.ToJson(document));}
            catch(Exception ex){message=ex.Message;yield break;}
            LocalAudioLoader operation=null;
            AudioClip candidateMusic=defaultMusic;
            string candidateHash=defaultHash;
            // === 摘要在解码前后核对；路径变更不能悄悄改变歌曲身份 ===
            try {if(!string.IsNullOrWhiteSpace(document.audioPath))candidateHash=DemoChartStore.HashFile(document.audioPath);}
            catch(Exception ex){message=ex.Message;yield break;}
            if(!string.IsNullOrWhiteSpace(document.audioPath))
            {
                operation=new LocalAudioLoader();loader=operation;
                yield return operation.Load(document.audioPath);
                if(loader!=operation){operation.Dispose();yield break;}
                if(operation.State!=AudioLoadState.Succeeded)
                {message=operation.Error ?? operation.State.ToString();operation.Dispose();loader=null;yield break;}
                candidateMusic=operation.Clip;
            }
            ChartSession candidate=null;
            try
            {
                if(!string.IsNullOrWhiteSpace(document.audioPath) && candidateHash!=DemoChartStore.HashFile(document.audioPath))
                    throw new IOException("Music changed during loading");
                if(acceptNewMusic)
                {
                    document.audioHash=candidateHash;
                    document.audioId=(string.IsNullOrWhiteSpace(document.audioPath)?"pcm:":"sha256:")+candidateHash;
                    document.duration=candidateMusic.length;
                }
                else DemoChartStore.VerifyIdentity(document,candidateHash,candidateMusic.length);
                candidate=ChartSession.FromDocument(document,candidateMusic);
                if(!candidate.Lanes.Exists(lane=>lane.laneId=="notes") || !candidate.Lanes.Exists(lane=>lane.laneId=="effects"))
                    throw new ArgumentException("This sample requires notes and effects lanes");
                ReplaceSession(candidate);candidate=null;
                music=candidateMusic;
                // === Conductor 切换完成后才释放旧音频；默认音乐保留供无路径存档恢复 ===
                loadedAudio?.Dispose();loadedAudio=operation;loader=null;
                pendingDocument=null;score=misses=0;message="Chart and audio loaded";
            }
            catch(Exception ex)
            {candidate?.Dispose();operation?.Dispose();loader=null;message=ex.Message;}
        }
        private void ReplaceSession(ChartSession replacement)
        {
            try { conductor.Configure(conductor.AudioSource,replacement.Source,replacement.Lanes); }
            catch { replacement.Dispose();throw; }
            judgementClock.Reset();tailPaused=false;
            session.Dispose();session=replacement;visible.Clear();judged.Clear();message="Chart restored";
        }
        private void Try(Action action) { try{action();}catch(Exception e){message=e.Message;} }
        private IEnumerator Smoke()
        {
            conductor.Play();float deadline=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<deadline)yield return null;
            bool passed=conductor.SongTime>3 && pulseCount>0 && misses>0;
            conductor.ClockCheck();
            Debug.Log($"[MetronomePlayer] song={conductor.SongTime:0.000}, effects={pulseCount}, misses={misses}, driftSamples={conductor.ClockDriftSamples}");
            conductor.Pause();double paused=conductor.SongTime;
            yield return null;passed &= conductor.SongTime==paused;
            try
            {
                DemoChartStore.Save(SavePath,ChartSession.ToJson(session.Source,session.Lanes));
                ReplaceSession(ChartSession.FromJson(File.ReadAllText(SavePath),music));
                Seek(1);passed &= Math.Abs(conductor.SongTime-1)<.001;
            }
            catch(Exception ex){Debug.LogException(ex);passed=false;}
            Debug.Log(passed?"[MetronomePlayer] SMOKE PASSED":"[MetronomePlayer] SMOKE FAILED");
            Application.Quit(passed?0:1);
        }
        // === 禁用即停止，不自动重播；先取消请求再终止协程 ===
        private void StopPlayback()
        {
            if(conductor)conductor.Stop();
            judgementClock.Reset();tailPaused=false;judgementTime=0;
            visible.Clear();judged.Clear();score=misses=0;
        }
        private void OnDisable()
        {
            StopPlayback();loader?.Dispose();loader=null;StopAllCoroutines();
        }
        private void OnDestroy()
        {
            if(conductor){conductor.EventApproaching-=OnApproaching;conductor.Unsubscribe("effects","pulse",OnPulse);conductor.Stop();}
            session?.Dispose();loader?.Dispose();loadedAudio?.Dispose();if(generated && defaultMusic)Destroy(defaultMusic);
        }
        private static AudioClip CreateSong()
        {
            const int rate=22050,seconds=24;var pcm=new float[rate*seconds];int[] melody={0,4,7,12,7,4,2,7};
            for(int i=0;i<pcm.Length;i++)
            {
                double t=(double)i/rate,beat=t%.5,envelope=Math.Exp(-beat*13);
                double frequency=220*Math.Pow(2,melody[(int)(t*2)%melody.Length]/12.0);
                pcm[i]=(float)(.25*Math.Sin(2*Math.PI*frequency*t)*envelope+.15*Math.Sin(2*Math.PI*70*t)*Math.Exp(-beat*35));
            }
            var clip=AudioClip.Create("Metronome original demo · 120 BPM",pcm.Length,1,rate,false);clip.SetData(pcm,0);return clip;
        }
    }
}
