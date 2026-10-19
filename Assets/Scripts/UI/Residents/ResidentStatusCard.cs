using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class ResidentStatusCard : MonoBehaviour
    {
        public ResidentsView owner;
        public RectTransform placementArea;
        public RectTransform[] reservedAreas;
        public Image portrait,preference;
        public Text number,details;
        public Text recent,pauseNote;
        public Button close,confirm,decline;
        public Text confirmLabel;
        public Button informationToggle,life;
        public Text informationLabel,focusName;
        public GameObject cluePanel;
        public Text[] clueLines;
        public bool startWithBehaviourClues;
        public bool ClueMode { get; private set; }
        // Each theme has ten coloured cells and its own resident-specific pressure threshold.
        public Image[] cells;
        public AttitudeFaceGraphic[] attitudes;
        public RectTransform[] thresholdMarkers;
        public Text[] overloadMarks;
        public RectTransform sidebarDock;
        public Vector2 thresholdRange;
        public bool illustrated;
        public GameObject[] coveredSidebar;
        bool[] sidebarWasActive;
        public float gap=10;
        public int SelectedResident { get; private set; }=-1;
        public Rect CurrentRect { get; private set; }
        readonly List<Rect> blocked=new List<Rect>();
        readonly Vector3[] corners=new Vector3[4];
        Rect lastTarget,lastViewport;
        Vector2 lastSize,lastAreaSize;
        int lastIndex=-1,lastReservedMask;
        bool placementValid;
        public int LayoutPassCount { get; private set; }
        RectTransform rect;
        void OnEnable()
        {
            if(coveredSidebar==null)return;
            sidebarWasActive=new bool[coveredSidebar.Length];
            for(int i=0;i<coveredSidebar.Length;i++)if(coveredSidebar[i]) { sidebarWasActive[i]=coveredSidebar[i].activeSelf;coveredSidebar[i].SetActive(false); }
        }
        void OnDisable()
        {
            if(sidebarWasActive==null)return;
            for(int i=0;i<coveredSidebar.Length;i++)if(coveredSidebar[i])coveredSidebar[i].SetActive(sidebarWasActive[i]);
            sidebarWasActive=null;
        }
        void Awake()
        {
            ClueMode=startWithBehaviourClues;
            rect=(RectTransform)transform;close.onClick.AddListener(Cancel);
            if(confirm)confirm.onClick.AddListener(()=>owner.ConfirmRecommendation());
            if(decline)decline.onClick.AddListener(Cancel);
            if(informationToggle)informationToggle.onClick.AddListener(ToggleInformation);
            if(life)life.onClick.AddListener(()=>owner.ShowResidentLife(SelectedResident));
        }
        void Cancel() { owner.CancelRecommendation(); }
        public void Show(int index)
        {
            if(!rect)rect=(RectTransform)transform;
            if(index==SelectedResident&&gameObject.activeSelf) { Cancel();return; }
            SelectedResident=index;placementValid=false;gameObject.SetActive(true);owner.HighlightResident(index);Refresh();Reposition();
        }
        public void Hide() { gameObject.SetActive(false);SelectedResident=-1;placementValid=false;if(owner)owner.HighlightResident(-1); }
        public void Refresh()
        {
            if(SelectedResident<0||owner.Run==null)return;
            var r=owner.Run.residents[SelectedResident];portrait.sprite=PortraitSprite();
            preference.sprite=owner.topicIcons[r.preferredTopic];number.text=(illustrated?"":"住户 ")+ResidentRun.RoomOf(SelectedResident);
            string topic=r.lastTopic<0?"还没有收到新的推送":ResidentRun.TopicNames[r.lastTopic]+" · "+owner.Run.SourceName(r.lastSource);
            details.text="正在："+owner.ActivityName(r.preferredTopic)+"\n最近反应："+r.reaction+"\n最近接收："+topic+"\n"+(r.recommended?"本阶段已推荐":"本阶段还可以推荐一次")+"\n准备推荐："+(owner.SelectedTopic>=0?ResidentRun.TopicNames[owner.SelectedTopic]:"请先选主题");
            var recent=owner.Run.events.Where(e=>e.resident==SelectedResident&&(e.kind=="玩家推荐"||e.kind=="邻居分享")).Reverse().Take(2).Reverse().ToArray();
            string trail=recent.Length==0?"最近两次：还没有新的接收记录":"最近两次："+string.Join(" / ",recent.Select(e=>ResidentRun.TopicNames[e.topic]+"←"+(e.source<0?"你":ResidentRun.RoomOf(e.source))));
            if(illustrated)
            {
                details.text="上次接收："+(r.lastTopic<0?"暂无":topic)+"\n当时反应："+r.reaction;
                trail=recent.Length==0?"看腻推送，也可能仍然喜欢它":string.Join("  /  ",recent.Select(e=>ResidentRun.TopicNames[e.topic]+"←"+(e.source<0?"你":ResidentRun.RoomOf(e.source))));
                RefreshPressure(r);
            }
            if(this.recent)this.recent.text=trail;else if(recent.Length>0)details.text+="\n"+trail;
            if(pauseNote)pauseNote.text=informationToggle?(ResidentLifeStories.GetWish(SelectedResident)!=""?ResidentLifeStories.FocusLabel(SelectedResident)+" · ":"")+"时间暂停":"观察中 · 时间暂停";
            confirm.interactable=owner.CanConfirm;
            confirmLabel.text=r.recommended?"本阶段已推荐":!owner.Run.Started&&owner.Run.TutorialDone?"先开始阶段":owner.SelectedTopic<0?"先选择主题":"确认推荐";
            if(illustrated&&owner.CanConfirm)confirmLabel.text="确认推荐 · "+ResidentRun.TopicNames[owner.SelectedTopic];
            if(informationLabel)informationLabel.text=ClueMode?"信息：行为线索":"信息：完整条格";
            if(cluePanel)cluePanel.SetActive(ClueMode);
            if(thresholdMarkers!=null)foreach(var marker in thresholdMarkers)if(marker)marker.gameObject.SetActive(!ClueMode);
            if(cells!=null)foreach(var cell in cells)if(cell)cell.enabled=!ClueMode;
            if(attitudes!=null)foreach(var face in attitudes)if(face)face.enabled=!ClueMode;
            if(overloadMarks!=null)foreach(var mark in overloadMarks)if(mark&&ClueMode)mark.gameObject.SetActive(false);
            if(clueLines!=null)for(int t=0;t<clueLines.Length;t++)
            {
                var seen=owner.Run.events.LastOrDefault(e=>e.resident==SelectedResident&&e.topic==t&&(e.kind=="玩家推荐"||e.kind=="邻居分享"));
                clueLines[t].text=seen==null?(r.preferredTopic==t?"正在做这件事":"还没看到接收反应"):
                    seen.reaction==ResidentReaction.Overloaded?"推送划走了":seen.reaction==ResidentReaction.Happy?"收到时很开心":seen.reaction==ResidentReaction.Curious?"认真看了几眼":"平静地划走了";
            }
            bool focus=ResidentLifeStories.GetWish(SelectedResident)!="";
            if(life)life.gameObject.SetActive(focus);
            if(focusName)focusName.text=focus?ResidentLifeStories.Name(SelectedResident):"";
            if(focus&&this.recent)this.recent.text=ResidentLifeStories.GetWish(SelectedResident);
        }
        public void ToggleInformation() { ClueMode=!ClueMode;Refresh(); }
        Sprite PortraitSprite()=>owner.catCare&&owner.catCare.residentIndex==SelectedResident&&owner.catCare.CareVisible?owner.catCare.body.sprite:owner.residents[SelectedResident].sprite;
        void RefreshPressure(ResidentState resident)
        {
            for(int topic=0;topic<5;topic++)
            {
                float value=Mathf.Clamp01(resident.saturation[topic])*10;
                for(int cell=0;cell<10;cell++)cells[topic*10+cell].fillAmount=Mathf.Clamp01(value-cell);
                var marker=thresholdMarkers[topic];var p=marker.anchoredPosition;
                p.x=Mathf.Lerp(thresholdRange.x,thresholdRange.y,Mathf.Clamp01(resident.aversionThresholds[topic]));marker.anchoredPosition=p;
                attitudes[topic].Attitude=resident.Attitude(topic);
                overloadMarks[topic].gameObject.SetActive(resident.PressureRatio(topic)>=1);
            }
        }
        void LateUpdate() { if(SelectedResident>=0)portrait.sprite=PortraitSprite(); }
        void OnRectTransformDimensionsChange() { placementValid=false;if(gameObject.activeSelf&&owner&&SelectedResident>=0)Reposition(); }
        public Rect InArea(RectTransform target)
        {
            target.GetWorldCorners(corners);
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach(var corner in corners) { Vector2 p=placementArea.InverseTransformPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        public void Reposition()
        {
            if(!rect||!placementArea||SelectedResident<0)return;
            if(sidebarDock)
            {
                Vector2 position=InArea(sidebarDock).center;
                if(!placementValid||rect.anchoredPosition!=position) { rect.anchoredPosition=position;LayoutPassCount++;placementValid=true; }
                CurrentRect=new Rect(position-rect.sizeDelta*.5f,rect.sizeDelta);return;
            }
            var visible=owner.ResidentViewport?InArea(owner.ResidentViewport):placementArea.rect;
            var targetRect=InArea(owner.residents[SelectedResident].rectTransform);int mask=0;
            for(int i=0;i<reservedAreas.Length;i++)if(reservedAreas[i]&&reservedAreas[i].gameObject.activeInHierarchy)mask|=1<<i;
            if(placementValid&&lastIndex==SelectedResident&&Near(lastTarget,targetRect)&&Near(lastViewport,visible)&&lastSize==rect.sizeDelta&&lastAreaSize==placementArea.rect.size&&lastReservedMask==mask)return;
            placementValid=true;lastIndex=SelectedResident;lastTarget=targetRect;lastViewport=visible;lastSize=rect.sizeDelta;lastAreaSize=placementArea.rect.size;lastReservedMask=mask;LayoutPassCount++;
            blocked.Clear();
            foreach(var actor in owner.residents) { if(!actor.gameObject.activeInHierarchy)continue;Rect area=InArea(actor.rectTransform);if(area.Overlaps(visible))blocked.Add(area); }
            foreach(var reserved in reservedAreas)if(reserved&&reserved.gameObject.activeInHierarchy)blocked.Add(InArea(reserved));
            Rect bounds=placementArea.rect;bounds.xMin+=8;bounds.xMax-=8;bounds.yMin+=8;bounds.yMax-=8;
            Vector2 size=rect.sizeDelta,best=Vector2.zero,target=targetRect.center;float score=float.PositiveInfinity;
            float step=UIFeedbackProfile.Default?UIFeedbackProfile.Default.placementStep:20;
            for(float x=bounds.xMin+size.x*.5f;x<=bounds.xMax-size.x*.5f;x+=step)
            for(float y=bounds.yMin+size.y*.5f;y<=bounds.yMax-size.y*.5f;y+=step)
            {
                Rect candidate=new Rect(new Vector2(x,y)-size*.5f,size);bool overlaps=false;
                foreach(var block in blocked) { var b=block;b.xMin-=6;b.xMax+=6;b.yMin-=6;b.yMax+=6;if(candidate.Overlaps(b)) { overlaps=true;break; } }
                float distance=(new Vector2(x,y)-target).sqrMagnitude;
                if(!overlaps&&distance<score) { score=distance;best=new Vector2(x,y); }
            }
            if(float.IsPositiveInfinity(score))
            {
                // Keep actors visible; dock above the topic controls in the side panel if necessary.
                best=new Vector2(620,70);
            }
            rect.anchoredPosition=best;CurrentRect=new Rect(best-size*.5f,size);
        }
        static bool Near(Rect a,Rect b)=>(a.center-b.center).sqrMagnitude<.25f&&(a.size-b.size).sqrMagnitude<.25f;
    }
}
