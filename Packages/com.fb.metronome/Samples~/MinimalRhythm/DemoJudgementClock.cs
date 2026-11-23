using System;
using FB.Metronome.Conductor;
namespace FB.Metronome.Samples
{
    /// <summary>玩法时钟：只在正常播放和片尾结算时前进；尾段暂停独立于已经 Ended 的音源。</summary>
    public sealed class DemoJudgementClock
    {
        private double anchorSong, anchorDsp, frozen;
        private bool anchored, tailPaused, completed;
        public void Reset() {anchored=false;tailPaused=false;completed=false;frozen=0;}
        public void PauseTail(double dsp)
        {if(anchored && !tailPaused){frozen=anchorSong+Math.Max(0,dsp-anchorDsp);tailPaused=true;}}
        public void ResumeTail(double dsp)
        {if(tailPaused){anchorSong=frozen;anchorDsp=dsp;tailPaused=false;}}
        public bool TryTime(MetroConductor.PlaybackState state,double song,double dsp,double duration,double latencySeconds,out double audible)
        {
            audible=song-latencySeconds;
            if(completed)return false;
            if(state==MetroConductor.PlaybackState.Playing)
            {
                // 到片尾的那一帧可能已经被 SongTime 钳制，保留上一帧的 DSP 锚点。
                if(song<duration || !anchored){anchorSong=song;anchorDsp=dsp;anchored=true;}
            }
            else if(state==MetroConductor.PlaybackState.Ended)
            {
                if(!anchored){anchorSong=duration;anchorDsp=dsp;anchored=true;}
            }
            else return false;
            double raw=tailPaused?frozen:anchorSong+Math.Max(0,dsp-anchorDsp);
            audible=raw-latencySeconds;
            if(!tailPaused && audible>duration+.15)completed=true;
            return !tailPaused;
        }
    }
}
