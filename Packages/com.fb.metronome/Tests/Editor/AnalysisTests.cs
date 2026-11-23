using FB.Metronome.Analysis;
using NUnit.Framework;
using UnityEngine;
namespace FB.Metronome.Tests.Editor
{
    public class AnalysisTests
    {
        [Test] public void MonoStereoShareTempoAndSilenceFails()
        {
            const int rate=8000,frames=rate*12;
            var mono=AudioClip.Create("mono",frames,1,rate,false);
            var stereo=AudioClip.Create("stereo",frames,2,rate,false);
            try
            {
                Assert.AreEqual(-1,UniBpmAnalyzer.AnalyzeBpm(mono));
                var a=new float[frames];var b=new float[frames*2];
                for(int i=0;i<frames;i++) { a[i]=i%(rate/2)<80?.8f:0;b[2*i]=a[i];b[2*i+1]=-a[i]; }
                mono.SetData(a,0);stereo.SetData(b,0);
                Assert.AreEqual(120,UniBpmAnalyzer.AnalyzeBpm(mono));
                Assert.AreEqual(UniBpmAnalyzer.AnalyzeBpm(mono),UniBpmAnalyzer.AnalyzeBpm(stereo));
            }
            finally { Object.DestroyImmediate(mono);Object.DestroyImmediate(stereo); }
        }
    }
}
