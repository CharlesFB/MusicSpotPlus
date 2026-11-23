using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using FB.Metronome.Conductor;
using FB.Metronome.Data;
using FB.Metronome.Loading;
using FB.Metronome.Playback;
using FB.Metronome.Samples;
using FB.Metronome.Serialization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace FB.Metronome.Tests.P0
{
    public class P0ReliabilityTests
    {
        private string directory;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"metro-p0-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void Cleanup(){Directory.Delete(directory,true);}
        private string Json(float bpm=120)=>JsonUtility.ToJson(new ChartDocument{duration=1,bpm=bpm});
        [Test] public void AtomicSaveRejectsBadInputAndRecoversPreviousBackup()
        {
            string path=Path.Combine(directory,"song.json");
            DemoChartStore.Save(path,Json());DemoChartStore.Save(path,Json(130));
            Assert.Throws<ArgumentException>(()=>DemoChartStore.Save(path,Json(0)));
            Assert.AreEqual(130,DemoChartStore.Read(path,out _).bpm);
            File.WriteAllText(path,"broken");
            Assert.AreEqual(120,DemoChartStore.Read(path,out bool recovered).bpm);Assert.IsTrue(recovered);
            DemoChartStore.Save(path,Json(140));
            Assert.AreEqual(120,ChartSession.ReadDocument(File.ReadAllText(path+".bak")).bpm);
            Assert.AreEqual(140,DemoChartStore.Read(path,out _).bpm);
            Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));
        }
        [Test] public void FailedReplacementKeepsExistingDocument()
        {
            string path=Path.Combine(directory,"song.json");DemoChartStore.Save(path,Json());
            Directory.CreateDirectory(path+".bak");
            Assert.Catch(()=>DemoChartStore.Save(path,Json(130)));
            Assert.AreEqual(120,DemoChartStore.Read(path,out _).bpm);
            Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));
        }
        [Test] public void MusicRelocationRequiresSameContentAndDuration()
        {
            string a=Path.Combine(directory,"a.wav"),b=Path.Combine(directory,"b.wav");
            File.WriteAllText(a,"audio");string hash=DemoChartStore.HashFile(a);
            var doc=new ChartDocument{audioId="song",audioHash=hash,duration=1};
            File.Move(a,b);Assert.DoesNotThrow(()=>DemoChartStore.VerifyIdentity(doc,DemoChartStore.HashFile(b),1));
            File.WriteAllText(b,"other");Assert.Throws<InvalidDataException>(()=>DemoChartStore.VerifyIdentity(doc,DemoChartStore.HashFile(b),1));
            Assert.Throws<InvalidDataException>(()=>DemoChartStore.VerifyIdentity(doc,hash,2));
            Assert.Throws<InvalidDataException>(()=>DemoChartStore.VerifyIdentity(new ChartDocument(),hash,1));
        }
        [TestCase(.2)] [TestCase(-.05)]
        public void TailClockRetainsLateWindowFreezesAndResets(double latency)
        {
            var clock=new DemoJudgementClock();
            Assert.IsTrue(clock.TryTime(MetroConductor.PlaybackState.Playing,.7,10,1,latency,out _));
            double endDsp=10.3;
            Assert.IsTrue(clock.TryTime(MetroConductor.PlaybackState.Ended,1,endDsp,1,latency,out double time));
            Assert.AreEqual(1-latency,time,1e-8);
            clock.PauseTail(endDsp);
            Assert.IsFalse(clock.TryTime(MetroConductor.PlaybackState.Ended,1,20,1,latency,out time));
            Assert.AreEqual(1-latency,time,1e-8);
            clock.ResumeTail(20);
            clock.TryTime(MetroConductor.PlaybackState.Ended,1,20.5,1,latency,out time);
            Assert.Greater(time,1.15); // final tick must be delivered so tail notes can become Miss
            Assert.IsFalse(clock.TryTime(MetroConductor.PlaybackState.Ended,1,21,1,latency,out _));
            clock.Reset();Assert.IsFalse(clock.TryTime(MetroConductor.PlaybackState.Stopped,0,22,1,latency,out _));
            Assert.IsTrue(clock.TryTime(MetroConductor.PlaybackState.Playing,0,23,1,latency,out _));
            Assert.IsFalse(clock.TryTime(MetroConductor.PlaybackState.Paused,0,24,1,latency,out _));
        }
        [Test] public void InvalidParametersRejectedBeforeMutatingConductor()
        {
            var go=new GameObject();var conductor=go.AddComponent<MetroConductor>();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.ahead=float.NaN);
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.ahead=-1);
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.clockCheckInterval=0);
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.clockCheckInterval=double.PositiveInfinity);
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.calibrationMs=double.NaN);
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.LookAheadSeconds=-1);
                Assert.Throws<ArgumentOutOfRangeException>(()=>new AudioLatency(-1,0));
                Assert.AreEqual(.2f,conductor.ahead);
                Set(conductor,"schedulingAhead",float.NaN); // invalid serialized data bypassing setter
                Assert.Throws<ArgumentOutOfRangeException>(()=>conductor.Play());
                Assert.AreEqual(MetroConductor.PlaybackState.Stopped,conductor.State);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        private string Wav(string name="test.wav")
        {
            string path=Path.Combine(directory,name);
            using(var w=new BinaryWriter(File.Create(path)))
            {
                const int rate=8000,count=8000;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);
                w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int i=0;i<count;i++)w.Write((short)(Math.Sin(i*.1)*1000));
            }
            return path;
        }
        private static IEnumerator Pump(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);double deadline=UnityEditor.EditorApplication.timeSinceStartup+10;
            try
            {
                while(stack.Count>0)
                {
                    Assert.Less(UnityEditor.EditorApplication.timeSinceStartup,deadline,"Coroutine did not terminate");
                    var current=stack.Peek();
                    if(!current.MoveNext()){(stack.Pop() as IDisposable)?.Dispose();continue;}
                    if(current.Current is IEnumerator nested)stack.Push(nested);else yield return null;
                }
            }
            finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }
        [UnityTest] public IEnumerator LoaderCancellationFailureAndOwnership()
        {
            string path=Wav();
            using(var canceled=new LocalAudioLoader())
            {yield return Pump(canceled.Load(path,new CancellationToken(true)));Assert.AreEqual(AudioLoadState.Canceled,canceled.State);}
            var active=new LocalAudioLoader();var routine=active.Load(path);
            routine.MoveNext();active.Dispose();active.Dispose();yield return Pump(routine);
            Assert.AreEqual(AudioLoadState.Canceled,active.State);
            using(var unsupported=new LocalAudioLoader())
            {
                var other=Path.ChangeExtension(path,"txt");File.Copy(path,other);
                yield return Pump(unsupported.Load(other));Assert.AreEqual(AudioLoadState.Failed,unsupported.State);
            }
            File.Delete(path);
            using(var deleted=new LocalAudioLoader())
            {yield return Pump(deleted.Load(path));Assert.AreEqual(AudioLoadState.Failed,deleted.State);}
            path=Wav();
            using(var good=new LocalAudioLoader())
            {yield return Pump(good.Load(path));Assert.AreEqual(AudioLoadState.Succeeded,good.State);Assert.IsNotNull(good.Clip);}
            var borrowed=AudioClip.Create("borrowed",8,1,8000,false);
            new LocalAudioLoader(borrowed).Dispose();Assert.IsTrue(borrowed);UnityEngine.Object.DestroyImmediate(borrowed);
        }
        [UnityTest] public IEnumerator DecoderFailureDoesNotRemainLoading()
        {
            string path=Path.Combine(directory,"broken.wav");File.WriteAllText(path,"not a wave file");
            bool previous=LogAssert.ignoreFailingMessages;
            using(var loader=new LocalAudioLoader())
            {
                try
                {
                    // Unity's native decoder logs an error in addition to returning the failure.
                    LogAssert.ignoreFailingMessages=true;
                    yield return Pump(loader.Load(path));
                    Assert.AreEqual(AudioLoadState.Failed,loader.State);
                    Assert.IsNull(loader.Clip);
                }
                finally{LogAssert.ignoreFailingMessages=previous;}
            }
        }
        private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);
        private static T Get<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        private static IEnumerator Restore(MinimalRhythmGame sample,ChartDocument doc)=>(IEnumerator)typeof(MinimalRhythmGame).GetMethod("RestoreAudio",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sample,new object[]{doc,false});
        [UnityTest] public IEnumerator RestoreFailureReplacementAndDisableKeepOwnershipConsistent()
        {
            var go=new GameObject();var sample=go.AddComponent<MinimalRhythmGame>();var output=go.AddComponent<AudioSource>();var conductor=go.AddComponent<MetroConductor>();
            var clip=AudioClip.Create("default",8000,1,8000,false);
            var doc=new ChartDocument{duration=1,audioId="default",audioHash=DemoChartStore.HashClip(clip)};
            doc.lanes.Add(new LaneDocument{laneId="effects"});doc.lanes.Add(new LaneDocument{laneId="notes"});
            var original=ChartSession.FromDocument(doc,clip);conductor.Configure(output,original.Source,original.Lanes);
            sample.music=clip;Set(sample,"defaultMusic",clip);Set(sample,"defaultHash",doc.audioHash);Set(sample,"session",original);Set(sample,"conductor",conductor);
            try
            {
                doc.audioPath=Path.Combine(directory,"missing.wav");
                yield return Pump(Restore(sample,doc));Assert.AreSame(original.Source,conductor.Source);Assert.AreSame(clip,sample.music);
                doc.audioPath=Wav();doc.audioHash="wrong";
                yield return Pump(Restore(sample,doc));Assert.AreSame(original.Source,conductor.Source);Assert.AreSame(clip,sample.music);
                doc.audioHash=DemoChartStore.HashFile(doc.audioPath);
                var first=Restore(sample,doc);Assert.IsTrue(first.MoveNext());
                var second=Restore(sample,doc);Assert.IsTrue(second.MoveNext()); // replaces first pending request
                yield return Pump((IEnumerator)first.Current);yield return Pump(first);
                yield return Pump((IEnumerator)second.Current);yield return Pump(second);
                Assert.AreNotSame(original.Source,conductor.Source);Assert.AreNotSame(clip,sample.music);
                Assert.AreEqual("notes",Get<ChartSession>(sample,"session").Lanes[1].laneId);
                var pending=Restore(sample,doc);Assert.IsTrue(pending.MoveNext());
                var pendingLoader=Get<LocalAudioLoader>(sample,"loader");
                // Start the yielded load, then simulate disabling the coroutine host.
                var nested=(IEnumerator)pending.Current;nested.MoveNext();
                typeof(MinimalRhythmGame).GetMethod("OnDisable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(sample,null);
                Assert.AreEqual(AudioLoadState.Canceled,pendingLoader.State);
                yield return Pump(nested);yield return Pump(pending);
                Assert.AreEqual(MetroConductor.PlaybackState.Stopped,conductor.State);
            }
            finally {UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(clip);}
        }
    }
}
