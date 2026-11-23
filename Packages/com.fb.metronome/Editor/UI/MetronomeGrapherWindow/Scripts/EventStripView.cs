using System;
using System.Collections.Generic;
using FB.Metronome.Data;
using UnityEngine;
using UnityEngine.UIElements;
namespace FB.Metronome.Editor.UI
{
    // === 横向事件条只绑定视口附近的卡片；列表大小不决定控件数量 ===
    internal sealed class EventStripView
    {
        private readonly ScrollView scroll;
        private readonly VisualTreeAsset template;
        private readonly Action<MetroEvent> clicked;
        private readonly List<EventItem> pool = new List<EventItem>();
        private readonly List<MetroEvent> data = new List<MetroEvent>();
        private MetroEvent selected;
        private const float Width=90;
        public EventStripView(ScrollView scroll, VisualTreeAsset template, Action<MetroEvent> clicked)
        {
            this.scroll=scroll;this.template=template;this.clicked=clicked;
            scroll.horizontalScroller.valueChanged += _=>Render();
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_=>Render());
        }
        public void Bind(IEnumerable<MetroEvent> events, MetroEvent selection)
        {
            data.Clear();if(events!=null)foreach(var e in events)if(e!=null)data.Add(e);
            selected=selection;
            scroll.contentContainer.style.width=data.Count*Width;
            Render();
        }
        public void Select(MetroEvent e, bool reveal)
        {
            selected=e;
            int index=data.IndexOf(e);
            if(reveal && index>=0) scroll.scrollOffset=new Vector2(Mathf.Max(0,index*Width-Width),0);
            Render();
        }
        private void Render()
        {
            float viewport=scroll.contentViewport.layout.width;
            if(float.IsNaN(viewport)||viewport<=0)viewport=900;
            int first=Mathf.Clamp((int)(scroll.scrollOffset.x/Width)-1,0,Mathf.Max(0,data.Count-1));
            int count=Mathf.Min(data.Count-first,Mathf.CeilToInt(viewport/Width)+3);
            while(pool.Count<count)
            {
                var item=new EventItem(template);item.style.position=Position.Absolute;
                item.Clicked+=i=>clicked(i.Event);pool.Add(item);
            }
            for(int i=0;i<pool.Count;i++)
            {
                var item=pool[i];
                if(i>=count){item.RemoveFromHierarchy();continue;}
                if(item.parent==null)scroll.Add(item);
                item.style.left=(first+i)*Width;
                item.Bind(data[first+i]);item.SetSelected(data[first+i]==selected);
            }
        }
    }
}
