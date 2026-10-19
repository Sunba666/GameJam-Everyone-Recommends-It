using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // A short presentation of saved events. No recommendation, probability or simulation time is replayed.
    public sealed class SpreadStoryView : MonoBehaviour
    {
        public ResidentsView owner;
        public CanvasGroup panel;
        public Text heading,body,subtitle,statusLabel,clockLabel,pauseLabel;
        public Image theme;
        public Button pause,skip,back;
        public DeliveryArrowGraphic arrow;
        [Range(10,15)] public float duration=12;
        public bool IsActive { get; private set; }
        public bool PlaybackPaused { get; private set; }
        public float HistoricalElapsed { get; private set; }=-1;
        public float PlaybackElapsed { get; private set; }
        public int DisplayedStage { get; private set; }
        public RunEvent CurrentEvent { get; private set; }
        public int DirectRecommendationCount { get; private set; }
        public int NeighbourShareCount { get; private set; }
        public IReadOnlyList<RunEvent> SelectedEvents=>sequence.AsReadOnly();

        readonly List<RunEvent> sequence=new List<RunEvent>();
        readonly List<ResidentState[]> before=new List<ResidentState[]>();
        Outline[] outlines;
        bool[] outlineEnabled;
        Color[] outlineColors;
        Vector2[] outlineDistances;
        GameObject[] coveredControls;
        bool[] controlWasActive;
        int originalFloor,currentIndex=-1,currentBeat=-1;
        bool focused=true;
        Action returnAction;
        ResidentRun presentedRun;
        float storySeconds,conclusionSeconds=3;

        void Awake()
        {
            if(!owner)owner=GetComponent<ResidentsView>();
            if(pause)pause.onClick.AddListener(TogglePlayback);
            if(skip)skip.onClick.AddListener(SkipToConclusion);
            if(back)back.onClick.AddListener(()=>Stop(true));
            if(panel)panel.gameObject.SetActive(false);
            if(arrow)arrow.gameObject.SetActive(false);
        }

        public bool HasShare(int stage)
        {
            return owner&&owner.Run!=null&&owner.Run.events.Any(e=>e.stage==stage&&ValidShare(e,owner.Run));
        }

        static bool ValidShare(RunEvent entry,ResidentRun run)
        {
            return entry.kind=="邻居分享"&&entry.source>=0&&entry.source<run.residents.Length&&
                entry.resident>=0&&entry.resident<run.residents.Length&&entry.topic>=0&&entry.topic<5&&
                entry.after!=null&&entry.after.Length==run.residents.Length;
        }

        public bool Begin(int stage,Action onReturn)
        {
            if(IsActive||!owner||owner.Run==null||!panel||!HasShare(stage))return false;
            var run=owner.Run;
            var shares=run.events.Where(e=>e.stage==stage&&ValidShare(e,run)).ToList();
            // Prefer an observable activity change; otherwise show the latest actual reception.
            var focal=shares.LastOrDefault(e=>e.previousTopic>=0&&e.currentTopic!=e.previousTopic)??shares[shares.Count-1];
            sequence.Clear();before.Clear();
            var earlier=shares.LastOrDefault(e=>e.tick<focal.tick&&e.resident==focal.source&&e.topic==focal.topic);
            if(earlier!=null)sequence.Add(earlier);
            sequence.Add(focal);
            foreach(var entry in sequence)
            {
                int index=run.events.IndexOf(entry);
                var previous=index>0?run.events[index-1]:null;
                var snapshot=previous!=null?previous.after:run.opening;
                if(snapshot==null||snapshot.Length!=run.residents.Length)snapshot=run.opening;
                // A stage's first reception can follow the previous stage-end snapshot.
                // Clone presentation data so old opportunities are not shown as used today.
                var display=snapshot.Select(state=>state.Copy()).ToArray();
                bool crossedStage=previous!=null&&previous.stage!=entry.stage;
                for(int resident=0;resident<display.Length;resident++)
                {
                    if(crossedStage)display[resident].recommended=display[resident].shared=display[resident].receivedShare=false;
                    display[resident].propsUntil=(int[])entry.after[resident].propsUntil.Clone();
                }
                before.Add(display);
            }
            // Include the tutorial (source -1) in the direct count, even though the stage clock was paused.
            DirectRecommendationCount=run.events.Count(e=>e.stage==stage&&e.source==-1&&e.resident>=0&&
                (e.kind=="玩家推荐"||e.kind=="邻居分享"));
            NeighbourShareCount=shares.Count;
            originalFloor=owner.SelectedFloor;SaveOutlines();
            presentedRun=run;returnAction=onReturn;DisplayedStage=stage;
            PlaybackElapsed=0;PlaybackPaused=false;currentIndex=currentBeat=-1;
            duration=Mathf.Clamp(duration,10,15);storySeconds=duration-conclusionSeconds;
            IsActive=true;
            if(owner.statusCard)owner.statusCard.Hide();
            if(owner.overlay)owner.overlay.Hide();
            coveredControls=owner.statusCard.coveredSidebar.Concat(owner.topics.Select(b=>b.gameObject)).Distinct().ToArray();
            controlWasActive=coveredControls.Select(g=>g.activeSelf).ToArray();
            foreach(var control in coveredControls)control.SetActive(false);
            panel.gameObject.SetActive(true);panel.alpha=1;panel.interactable=panel.blocksRaycasts=true;
            if(pauseLabel)pauseLabel.text="暂停片段";
            Present();
            return true;
        }

        void Update()
        {
            if(!IsActive)return;
            if(!owner||owner.Run!=presentedRun) { Stop(false);return; }
            // Do not use owner.EffectsPaused here: the story itself is a world-pause reason.
            bool suspended=!focused||(owner.settingsPanel&&owner.settingsPanel.activeSelf)||
                (FrontendServices.Instance&&FrontendServices.Instance.IsTransitioning);
            if(!PlaybackPaused&&!suspended)PlaybackElapsed+=Time.unscaledDeltaTime;
            if(PlaybackElapsed>=duration) { Stop(true);return; }
            Present();
        }

        void Present()
        {
            if(clockLabel)clockLabel.text="已发生的记录 · "+Mathf.CeilToInt(Mathf.Max(0,duration-PlaybackElapsed))+" 秒";
            if(PlaybackElapsed>=storySeconds) { PresentConclusion();return; }
            float eventSeconds=storySeconds/sequence.Count;
            int index=Mathf.Min(sequence.Count-1,(int)(PlaybackElapsed/eventSeconds));
            float eventTime=PlaybackElapsed-index*eventSeconds;
            float part=eventTime/eventSeconds;
            int beat=part<.28f?0:part<.52f?1:2;
            var entry=sequence[index];CurrentEvent=entry;HistoricalElapsed=entry.tick*.1f;
            if(currentIndex!=index)
            {
                currentIndex=index;currentBeat=-1;
                owner.SelectFloor(ResidentRun.FloorOf(entry.resident));
                if(theme) { theme.sprite=owner.topicIcons[entry.topic];theme.gameObject.SetActive(true); }
                if(heading)heading.text="看看怎么传来的";
                if(statusLabel)statusLabel.text="第 "+DisplayedStage+" 阶段 · "+HistoricalElapsed.ToString("F1")+" 秒\n"+
                    ResidentRun.RoomOf(entry.source)+" → "+ResidentRun.RoomOf(entry.resident)+" · "+ResidentRun.TopicNames[entry.topic];
            }
            if(currentBeat!=beat)
            {
                currentBeat=beat;
                if(beat==0)
                {
                    owner.RenderStates(before[index],DisplayedStage);Highlight(entry.source,-1);
                    if(body)body.text="住户 "+ResidentRun.RoomOf(entry.source)+" 分享了「"+ResidentRun.TopicNames[entry.topic]+"」。\n\n先看看内容是从谁那里来的。"+
                        (sequence.Count>1&&index==1?"\n\n前一条也是同主题分享。这是两次分别发生的分享，按真实的先后顺序回看。":"");
                    if(subtitle)subtitle.text=ResidentRun.RoomOf(entry.source)+" 分享了「"+ResidentRun.TopicNames[entry.topic]+"」";
                }
                else if(beat==1)
                {
                    Highlight(entry.source,entry.resident);
                    if(body)body.text="内容来自邻居 "+ResidentRun.RoomOf(entry.source)+"，送到住户 "+ResidentRun.RoomOf(entry.resident)+"。\n\n现在看看收到它之后发生了什么。";
                    if(subtitle)subtitle.text=ResidentRun.RoomOf(entry.source)+" → "+ResidentRun.RoomOf(entry.resident)+" · 邻居分享";
                }
                else
                {
                    owner.RenderStates(entry.after,DisplayedStage);Highlight(-1,entry.resident);
                    string previous=Activity(entry.previousTopic),current=Activity(entry.currentTopic);
                    bool changed=entry.previousTopic>=0&&entry.currentTopic>=0&&entry.previousTopic!=entry.currentTopic;
                    if(body)body.text="住户 "+ResidentRun.RoomOf(entry.resident)+" 收到内容。\n\n"+entry.after[entry.resident].reaction+
                        "\n\n"+(changed?"之前："+previous+"\n之后："+current:"他仍继续："+current+"\n收到内容并不保证换一种活动。");
                    if(subtitle)subtitle.text=ResidentRun.RoomOf(entry.resident)+(changed?" 从「"+previous+"」转向「"+current+"」":" 收到了分享，继续「"+current+"」");
                }
            }
            DrawArrow(entry,part,beat);
        }

        void PresentConclusion()
        {
            if(currentBeat==3)return;
            currentBeat=3;CurrentEvent=sequence[sequence.Count-1];HistoricalElapsed=CurrentEvent.tick*.1f;
            owner.RenderStates(CurrentEvent.after,DisplayedStage);Highlight(-1,CurrentEvent.resident);
            if(arrow)arrow.gameObject.SetActive(false);
            if(theme)theme.gameObject.SetActive(false);
            if(heading)heading.text="这一阶段，楼里自己在分享";
            string direct=DirectRecommendationCount==0?"这一阶段，你根本没有再推。":"这一阶段，你直接推荐了 "+DirectRecommendationCount+" 次。";
            if(body)body.text=direct+"\n\n邻居实际分享了 "+NeighbourShareCount+" 次。\n\n"+
                (DirectRecommendationCount==0?"你选择停下来观察，生活和邻里仍继续发生变化。":"邻居分享也参与了这次结果。"+
                    (DisplayedStage==1?"教学推荐同样计入你的次数。":""))+"\n\n片段结束后返回正式结果。";
            if(subtitle)subtitle.text=direct+" 邻居分享 "+NeighbourShareCount+" 次。";
        }

        void DrawArrow(RunEvent entry,float part,int beat)
        {
            if(!arrow)return;
            bool visible=beat==1||beat==2;arrow.gameObject.SetActive(visible);
            if(!visible)return;
            var from=owner.residents[entry.source].rectTransform;var to=owner.residents[entry.resident].rectTransform;
            float direction=Mathf.Sign(to.position.x-from.position.x);
            var start=arrow.rectTransform.InverseTransformPoint(from.TransformPoint(new Vector3(direction*54,65,0)));
            var end=arrow.rectTransform.InverseTransformPoint(to.TransformPoint(new Vector3(-direction*54,65,0)));
            float progress=FrontendServices.Instance&&FrontendServices.Instance.settings.motion?Mathf.Clamp01((part-.28f)/.24f):1;
            Color tint=owner.delivery&&entry.topic<owner.delivery.themeColors.Length?owner.delivery.themeColors[entry.topic]:new Color(1,.40f,.28f);
            arrow.Draw(start,end,progress,tint,false,22);
        }

        string Activity(int topic)=>topic>=0&&topic<5?owner.ActivityName(topic):"原来的活动";

        void SaveOutlines()
        {
            outlines=new Outline[owner.residents.Length];outlineEnabled=new bool[outlines.Length];outlineColors=new Color[outlines.Length];outlineDistances=new Vector2[outlines.Length];
            for(int i=0;i<outlines.Length;i++)
            {
                outlines[i]=owner.residents[i].GetComponent<Outline>();
                if(!outlines[i])continue;outlineEnabled[i]=outlines[i].enabled;outlineColors[i]=outlines[i].effectColor;outlineDistances[i]=outlines[i].effectDistance;
            }
        }

        void Highlight(int source,int receiver)
        {
            for(int i=0;i<outlines.Length;i++)
            {
                if(!outlines[i])continue;
                outlines[i].enabled=i==source||i==receiver;
                outlines[i].effectColor=i==receiver?new Color(1,.48f,.24f):new Color(.26f,.64f,.60f);
                outlines[i].effectDistance=new Vector2(5,-5);
            }
        }

        public void TogglePlayback()
        {
            if(!IsActive)return;PlaybackPaused=!PlaybackPaused;
            if(pauseLabel)pauseLabel.text=PlaybackPaused?"继续片段":"暂停片段";
        }

        public void SkipToConclusion()
        {
            if(!IsActive)return;PlaybackElapsed=storySeconds;PlaybackPaused=false;
            if(pauseLabel)pauseLabel.text="暂停片段";Present();
        }

        public void Stop(bool invokeReturn=false)
        {
            if(!IsActive)return;
            IsActive=false;PlaybackPaused=false;HistoricalElapsed=-1;CurrentEvent=null;
            if(arrow)arrow.gameObject.SetActive(false);
            if(panel)panel.gameObject.SetActive(false);
            if(coveredControls!=null)for(int i=0;i<coveredControls.Length;i++)if(coveredControls[i])coveredControls[i].SetActive(controlWasActive[i]);
            coveredControls=null;controlWasActive=null;
            if(owner&&owner.Run!=null)
            {
                owner.RenderStates(owner.Run.residents);
                owner.SelectFloor(originalFloor);
            }
            if(outlines!=null)for(int i=0;i<outlines.Length;i++)if(outlines[i])
            { outlines[i].enabled=outlineEnabled[i];outlines[i].effectColor=outlineColors[i];outlines[i].effectDistance=outlineDistances[i]; }
            var callback=returnAction;returnAction=null;presentedRun=null;
            if(invokeReturn)callback?.Invoke();
        }

        void OnApplicationFocus(bool value) { focused=value; }
        void OnDisable() { Stop(false); }
    }
}
