using FB.Metronome.Conductor;
using FB.Metronome.Data;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FB.Metronome.Tests.Runtime
{
    /// <summary>
    /// P3 目前只完成订阅表。本文件测登记 / 退订，不测 SongTime 过线开火。
    /// </summary>
    public class MetroConductorSubscribeTests
    {
        private GameObject _go;
        private MetroConductor _conductor;
        private EventLane _tapLane;
        private EventLane _sfxLane;
        private MetroEvent _tapEvent;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SubscribeTest");
            _conductor = _go.AddComponent<MetroConductor>();

            _tapLane = ScriptableObject.CreateInstance<EventLane>();
            _tapLane.laneId = "tap";
            _sfxLane = ScriptableObject.CreateInstance<EventLane>();
            _sfxLane.laneId = "sfx";
            _tapEvent = new MetroEvent { time = 1f, eventId = "tap", payload = "left" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_tapLane != null)
                Object.DestroyImmediate(_tapLane);
            if (_sfxLane != null)
                Object.DestroyImmediate(_sfxLane);
        }

        [Test]
        public void Subscribe_ByEventId_HandlerRuns_UnsubscribeStops()
        {
            int count = 0;
            void OnTap(EventLane lane, MetroEvent e) => count++;

            _conductor.Subscribe("tap", OnTap);
            InvokeByEventId("tap", _tapLane, _tapEvent);
            Assert.AreEqual(1, count);

            _conductor.Unsubscribe("tap", OnTap);
            InvokeByEventId("tap", _tapLane, _tapEvent);
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Subscribe_LaneAndEvent_DoesNotShareTableWithEventIdOnly()
        {
            int anyTap = 0;
            int sfxTap = 0;
            void OnAnyTap(EventLane lane, MetroEvent e) => anyTap++;
            void OnSfxTap(EventLane lane, MetroEvent e) => sfxTap++;

            _conductor.Subscribe("tap", OnAnyTap);
            _conductor.Subscribe("sfx", "tap", OnSfxTap);

            InvokeByEventId("tap", _tapLane, _tapEvent);
            Assert.AreEqual(1, anyTap);
            Assert.AreEqual(0, sfxTap);

            InvokeByLaneEvent("sfx", "tap", _sfxLane, _tapEvent);
            Assert.AreEqual(1, anyTap);
            Assert.AreEqual(1, sfxTap);
        }

        [Test]
        public void SubscribeLane_FiresForThatLaneTable_Only()
        {
            int hold = 0;
            int tap = 0;
            void OnHold(EventLane lane, MetroEvent e) => hold++;
            void OnTapLane(EventLane lane, MetroEvent e) => tap++;

            _conductor.SubscribeLane("hold", OnHold);
            _conductor.SubscribeLane("tap", OnTapLane);

            InvokeByLane("hold", _tapLane, _tapEvent);
            Assert.AreEqual(1, hold);
            Assert.AreEqual(0, tap);
        }

        [Test]
        public void Subscribe_EmptyOrNull_DoesNotRegister()
        {
            int count = 0;
            void OnTap(EventLane lane, MetroEvent e) => count++;

            _conductor.Subscribe("", OnTap);
            _conductor.Subscribe("tap", null);
            _conductor.SubscribeLane("", OnTap);
            _conductor.Subscribe("sfx", "", OnTap);

            Assert.IsFalse(HasEventId("tap"));
            Assert.IsFalse(HasLane("sfx"));
            InvokeByEventId("tap", _tapLane, _tapEvent);
            Assert.AreEqual(0, count);
        }

        private void InvokeByEventId(string eventId, EventLane lane, MetroEvent e)
        {
            var map = GetPrivate<Dictionary<string, MetroConductor.MetroEventHandler>>("_byEventId");
            if (map.TryGetValue(eventId, out var handler))
                handler?.Invoke(lane, e);
        }

        private void InvokeByLane(string laneId, EventLane lane, MetroEvent e)
        {
            var map = GetPrivate<Dictionary<string, MetroConductor.MetroEventHandler>>("_byLane");
            if (map.TryGetValue(laneId, out var handler))
                handler?.Invoke(lane, e);
        }

        private void InvokeByLaneEvent(string laneId, string eventId, EventLane lane, MetroEvent e)
        {
            var map = GetPrivate<Dictionary<(string laneId, string eventId), MetroConductor.MetroEventHandler>>("_byLaneEvent");
            if (map.TryGetValue((laneId, eventId), out var handler))
                handler?.Invoke(lane, e);
        }

        private bool HasEventId(string eventId)
        {
            var map = GetPrivate<Dictionary<string, MetroConductor.MetroEventHandler>>("_byEventId");
            return map.ContainsKey(eventId);
        }

        private bool HasLane(string laneId)
        {
            var map = GetPrivate<Dictionary<string, MetroConductor.MetroEventHandler>>("_byLane");
            return map.ContainsKey(laneId);
        }

        private T GetPrivate<T>(string fieldName)
        {
            FieldInfo field = typeof(MetroConductor).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(_conductor);
        }
    }
}
