using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
namespace FB.Metronome.Loading
{
    public enum AudioLoadState { Idle, Loading, Succeeded, Canceled, Failed }
    /// <summary>在 Unity 主线程启动 Load 协程。Dispose 取消请求并释放拥有的 Clip；调用方提供的 Clip 不归加载器所有。</summary>
    public sealed class LocalAudioLoader : IDisposable
    {
        public AudioLoadState State { get; private set; }
        public AudioClip Clip { get; private set; }
        public string Error { get; private set; }
        private UnityWebRequest request;
        private bool disposed, ownsClip;
        public LocalAudioLoader(AudioClip existing = null)
        { Clip=existing;State=existing?AudioLoadState.Succeeded:AudioLoadState.Idle; }
        public IEnumerator Load(string path, CancellationToken cancellation = default)
        {
            if(disposed){State=AudioLoadState.Canceled;yield break;}
            if (State != AudioLoadState.Idle) throw new InvalidOperationException("Use a new loader for each request");
            State=AudioLoadState.Loading;
            if (cancellation.IsCancellationRequested) { State=AudioLoadState.Canceled;yield break; }
            string uri=null;AudioType type=AudioType.UNKNOWN;
            try
            {
                if (!Path.IsPathRooted(path) || !File.Exists(path)) throw new FileNotFoundException("Audio path must be an existing absolute local file",path);
                switch(Path.GetExtension(path).ToLowerInvariant())
                { case ".wav":type=AudioType.WAV;break;case ".ogg":type=AudioType.OGGVORBIS;break;case ".mp3":type=AudioType.MPEG;break;default:throw new NotSupportedException("Supported formats: wav, ogg, mp3 (platform decoder dependent)"); }
                uri=new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
            catch(Exception ex) { Error=ex.Message;State=AudioLoadState.Failed; }
            if(State==AudioLoadState.Failed) yield break;
            UnityWebRequestAsyncOperation operation=null;
            try
            {
                request=UnityWebRequestMultimedia.GetAudioClip(uri,type);
                operation=request.SendWebRequest();
            }
            catch(Exception ex) { Error=ex.Message;State=AudioLoadState.Failed;ReleaseRequest(); }
            if(State==AudioLoadState.Failed)yield break;
            try
            {
                // === 先检查取消，再访问 operation，避免读取已 Dispose 的请求 ===
                while(!disposed && !cancellation.IsCancellationRequested && !operation.isDone) yield return null;
                if(disposed || cancellation.IsCancellationRequested)
                { State=AudioLoadState.Canceled;yield break; }
                try
                {
                    if(request.result!=UnityWebRequest.Result.Success)
                    { Error=request.error;State=AudioLoadState.Failed; }
                    else
                    {
                        Clip=DownloadHandlerAudioClip.GetContent(request);ownsClip=true;
                        // === 非空 Clip 不等于解码成功：某些解码器失败时仍返回空壳对象 ===
                        bool valid=Clip && Clip.loadState==AudioDataLoadState.Loaded && Clip.samples>0 && Clip.channels>0 && Clip.frequency>0;
                        State=valid?AudioLoadState.Succeeded:AudioLoadState.Failed;
                        if(!valid){Error="Decoder returned no usable audio data";ReleaseClip();}
                    }
                }
                catch(Exception ex) { Error=ex.Message;State=AudioLoadState.Failed; }
            }
            finally { if(State==AudioLoadState.Loading)State=AudioLoadState.Canceled; ReleaseRequest(); }
        }
        private void ReleaseRequest()
        {
            if(request==null)return;
            if(State==AudioLoadState.Canceled)request.Abort();
            request.Dispose();request=null;
        }
        private void ReleaseClip()
        {
            if(ownsClip && Clip) { if(Application.isPlaying) UnityEngine.Object.Destroy(Clip);else UnityEngine.Object.DestroyImmediate(Clip); }
            Clip=null;ownsClip=false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(State==AudioLoadState.Loading || State==AudioLoadState.Idle) { State=AudioLoadState.Canceled;ReleaseRequest(); }
            ReleaseClip();
        }
    }
}
