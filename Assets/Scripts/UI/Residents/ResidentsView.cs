using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EveryoneRecommends.Frontend
{
    public sealed class ResidentsView : MonoBehaviour
    {
        public Button[] topics,floors;
        public Button allFloors,returnToMenu,settingsButton,upFloor,downFloor,pauseButton,fastForward;
        public Button changeNotice;
        public Text changeNoticeLabel;
        public Button paceButton;
        public Text paceLabel;
        public Text[] roomLabels;
        public GameObject settingsPanel;
        public Image[] residents,statusIcons,propImages;
        public Sprite[] activities,topicIcons;
        public Text selectionLabel,feedback,targetLabel,pauseLabel;
        public Text[] reactionLabels,recommendedLabels;
        public ResidentReactionBubble[] reactionBubbles;
        public ResidentMotion[] motions;
        public ResidentStatusCard statusCard;
        public StageProgressView stageProgress;
        public DemoOverlay overlay;
        public RecommendationDeliveryView delivery;
        public RectTransform buildingViewport,buildingContent;
        public Image buildingTint;
        public ApartmentBackdrop apartmentBackdrop;
        public FloorDetailView floorDetails;
        public EndingCatalog endings;
        public SpreadStoryView spreadStory;
        public CatCareBeatView catCare;
        public StatePropMotionView statePropMotion;
        public RectTransform ResidentViewport=>floorDetails&&floorDetails.IsOpen?floorDetails.viewport:buildingViewport;
        [Min(1)]public float stageDuration=90;
        [Min(1)]public int totalStages=12;
        public SimulationParameters simulation=new SimulationParameters();
        public int SelectedFloor { get; private set; }
        public int SelectedTopic { get; private set; }=-1;
        public ResidentRun Run { get; private set; }
        bool focused=true,manualPause,replayPlaying,replayFinal,summaryWaiting;
        int paceIndex;
        float lastInteraction;
        int hudStamp=int.MinValue;
        public int HudRefreshCount { get; private set; }
        public float ObservationSpeed=>paceIndex==0?1:paceIndex==1?2:4;
        public bool EffectsPaused=>!focused||manualPause||settingsPanel.activeSelf||(spreadStory&&spreadStory.IsActive)||FrontendServices.Instance.IsTransitioning;
        int pendingTarget=-1,introStep,lastEvent,replayPosition;
        int noticeFloor;
        float replayClock;
        float replayLightingTime=-1;
        public string BackgroundTimeOfDay
        {
            get
            {
                float time=spreadStory&&spreadStory.IsActive?spreadStory.HistoricalElapsed:replayLightingTime>=0?replayLightingTime:Run.Elapsed;
                return time<Run.rules.daySeconds?"白天":time<Run.rules.daySeconds+Run.rules.duskSeconds?"黄昏":"晚上";
            }
        }
        float[] responseUntil;
        List<RunEvent> replayEvents=new List<RunEvent>();
        Vector2 cameraTarget=new Vector2(220,0);
        float zoomTarget=1;
        public bool PausedForWorld=>!focused||manualPause||settingsPanel.activeSelf||(spreadStory&&spreadStory.IsActive)||(overlay&&overlay.gameObject.activeSelf)||(statusCard&&statusCard.gameObject.activeSelf)||!Run.Started||Run.StageEnded||FrontendServices.Instance.IsTransitioning;
        public bool TimerPaused=>PausedForWorld;
        public string WorldPauseReason=>spreadStory&&spreadStory.IsActive?"观看传播 · 时间暂停":settingsPanel.activeSelf?"设置中 · 时间暂停":statusCard.gameObject.activeSelf?"观察住户 · 时间暂停":manualPause?"已暂停":!focused?"窗口未激活 · 时间暂停":overlay.gameObject.activeSelf?"查看记录 · 时间暂停":!Run.Started?"准备阶段 · 尚未计时":"已暂停";
        public bool CanConfirm=>!(spreadStory&&spreadStory.IsActive)&&statusCard&&statusCard.SelectedResident>=0&&SelectedTopic>=0&&!Run.StageEnded&&!manualPause&&focused&&!settingsPanel.activeSelf&&!FrontendServices.Instance.IsTransitioning&&!Run.residents[statusCard.SelectedResident].recommended&&Run.Target>=0&&(Run.Started||!Run.TutorialDone);
        static readonly int[] Activity={2,4,3,1,0};
        public string ActivityName(int topic)=>new[]{"刷猫视频","吃东西","打游戏","看报纸","听音乐"}[topic];
        void Awake()
        {
            Run=FrontendServices.Instance.EnsureRun(residents.Length,stageDuration,totalStages,simulation);
            responseUntil=new float[residents.Length];
            for(int i=0;i<topics.Length;i++) { int n=i;topics[i].onClick.AddListener(()=>SelectTopic(n)); }
            for(int i=0;i<floors.Length;i++) { int floor=i+1;floors[i].onClick.AddListener(()=>SelectFloor(floor)); }
            for(int i=0;i<residents.Length;i++)
            {
                int index=i;residents[i].GetComponent<Button>().onClick.AddListener(()=>ShowResident(index));
                statusIcons[i].GetComponent<Button>().onClick.AddListener(()=> { if(spreadStory&&spreadStory.IsActive)return;SelectFloor(ResidentRun.FloorOf(index));ShowResident(index); });
            }
            allFloors.onClick.AddListener(()=>SelectFloor(0));upFloor.onClick.AddListener(()=>SelectFloor(Mathf.Clamp((SelectedFloor==0?1:SelectedFloor)+1,1,3)));
            downFloor.onClick.AddListener(()=>SelectFloor(Mathf.Clamp((SelectedFloor==0?3:SelectedFloor)-1,1,3)));
            returnToMenu.onClick.AddListener(AskReturn);settingsButton.onClick.AddListener(ShowSettings);
            pauseButton.onClick.AddListener(TogglePause);fastForward.onClick.AddListener(AskFinishEarly);
            if(paceButton)paceButton.onClick.AddListener(CycleObservationSpeed);
            if(changeNotice) { changeNotice.onClick.AddListener(()=> { if(spreadStory&&spreadStory.IsActive)return;SelectFloor(noticeFloor);changeNotice.gameObject.SetActive(false); });changeNotice.gameObject.SetActive(false); }
            settingsPanel.SetActive(false);statusCard.Hide();overlay.Hide();SelectFloor(0);RenderStates(Run.residents);RefreshHud();ShowIntro();
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)&&!FrontendServices.Instance.IsTransitioning)
            {
                if(spreadStory&&spreadStory.IsActive)spreadStory.Stop(true);
                else if(settingsPanel.activeSelf)settingsPanel.SetActive(false);
                else if(statusCard.gameObject.activeSelf)CancelRecommendation();
                else if(!overlay.gameObject.activeSelf)TogglePause();
            }
            if(statusCard.gameObject.activeSelf&&Input.GetMouseButtonDown(0))
            {
                var p=new PointerEventData(EventSystem.current){position=Input.mousePosition};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
                if(hits.Count==0)CancelRecommendation();
            }
            MoveCamera();TickStage(Time.unscaledDeltaTime*ObservationSpeed);
            if(summaryWaiting&&!EffectsPaused&&(!delivery||!delivery.IsBusy)&&(!catCare||!catCare.IsPlaying))ShowSummary();
            if(Run.Started&&!Run.StageEnded&&!PausedForWorld&&Time.unscaledTime-lastInteraction>9)
                feedback.text=Run.Available==0?"这一轮都送到了。停一停看邻居分享，或切换观察速度减少等待。":"也可以换个主题，或暂不推荐。点击住户，比较最近两次接收后的反应。";
            if(replayPlaying&&focused&&!settingsPanel.activeSelf)
            {
                replayClock+=Time.unscaledDeltaTime;
                if(replayClock>=1.6f) { replayClock=0;if(replayPosition<replayEvents.Count-1) { replayPosition++;ShowReplayEvent(); }else { replayPlaying=false;ShowReplayEvent(); } }
            }
            for(int i=0;i<reactionLabels.Length;i++)if(reactionBubbles==null||i>=reactionBubbles.Length||!reactionBubbles[i])reactionLabels[i].gameObject.SetActive(Time.unscaledTime<responseUntil[i]);
        }
        void OnApplicationFocus(bool value) { focused=value; }
        void MoveCamera()
        {
            if(!buildingContent)return;
            bool motion=FrontendServices.Instance.settings.motion;float response=UIFeedbackProfile.Default?UIFeedbackProfile.Default.cameraResponse:8;
            buildingContent.anchoredPosition=motion?Vector2.Lerp(buildingContent.anchoredPosition,cameraTarget,1-Mathf.Exp(-response*Time.unscaledDeltaTime)):cameraTarget;
            buildingContent.localScale=motion?Vector3.Lerp(buildingContent.localScale,Vector3.one*zoomTarget,1-Mathf.Exp(-response*Time.unscaledDeltaTime)):Vector3.one*zoomTarget;
            if(statusCard.gameObject.activeSelf)statusCard.Reposition();
        }
        void ShowIntro()
        {
            if(Run.Started)return;
            if(introStep==0)
            {
                overlay.Show("302 的新邻居","刚搬来 302，还没和大家说上话。\n\n你最近喜欢看什么？给邻居也分享一条吧，说不定能聊起来。",new[]{"继续","跳过开场"},new Action[]{()=> { introStep++;SelectFloor(3);ShowIntro(); },ShowTarget});
            }
            else overlay.Show("大家都有自己的生活","看看另一层的邻居正在做什么。\n\n推荐可能带来共同话题，也可能被平静地划走。先选一个你想带火的主题。",new[]{"选择目标","跳过开场"},new Action[]{()=> { SelectFloor(2);ShowTarget(); },ShowTarget});
        }
        void ShowTarget()
        {
            if(Run.Target>=0) { overlay.Hide();return; }
            var names=ResidentRun.TopicNames.Concat(new[]{"确认目标"}).ToArray();var actions=new Action[6];
            for(int i=0;i<5;i++) { int topic=i;actions[i]=()=> { pendingTarget=topic;ShowTarget(); }; }
            actions[5]=()=>
            {
                if(pendingTarget<0)return;Run.SetTarget(pendingTarget);overlay.Hide();RefreshHud();
                feedback.text="先点击一位住户，选择主题并确认推荐。教学推荐会计入第 1 阶段。";
            };
            overlay.Show("这次想让什么成为潮流？","选定的目标在本局中保持不变。\n\n当前选择："+(pendingTarget<0?"尚未选择":ResidentRun.TopicNames[pendingTarget])+"\n\n选目标不会发送内容，也不会消耗推荐机会。\n也可以换话题或暂不推荐，机会不用全部用完。",names,actions);
            overlay.buttons[5].interactable=pendingTarget>=0;
        }
        public void ShowResident(int index)
        {
            if(index<0||index>=residents.Length||(spreadStory&&spreadStory.IsActive)||overlay.gameObject.activeSelf||manualPause||settingsPanel.activeSelf||FrontendServices.Instance.IsTransitioning)return;
            if(floorDetails&&floorDetails.IsTransitioning&&SelectedFloor>0) { floorDetails.QueueResident(index);return; }
            lastInteraction=Time.unscaledTime;
            SelectedTopic=-1;statusCard.Show(index);RefreshHud();
            if(ResidentLifeStories.GetWish(index)!="")feedback.text=ResidentLifeStories.FocusLabel(index)+"："+ResidentLifeStories.GetWish(index)+" 现在"+ResidentLifeStories.CurrentLine(Run,index);
        }
        public void SelectTopic(int topic)
        {
            if(topic<0||topic>=5||Run.StageEnded||manualPause||overlay.gameObject.activeSelf||!statusCard.gameObject.activeSelf)return;
            SelectedTopic=topic;RefreshHud();statusCard.Refresh();
            lastInteraction=Time.unscaledTime;
        }
        public bool ConfirmRecommendation()
        {
            if(!CanConfirm)return false;
            int index=statusCard.SelectedResident;bool sent=Run.Recommend(index,SelectedTopic);
            if(!sent)return false;
            lastInteraction=Time.unscaledTime;
            statusCard.Hide();SelectedTopic=-1;ReadNewEvents();RenderStates(Run.residents);RefreshHud();
            if(!Run.Started)feedback.text="教学推荐已送达，机会已记入第 1 阶段。点击顶部开始，剩余住户仍可推荐。";
            return true;
        }
        public void CancelRecommendation() { statusCard.Hide();SelectedTopic=-1;RefreshHud(); }
        public void SelectFloor(int floor)
        {
            int desired=Mathf.Clamp(floor,0,3);
            if(desired==SelectedFloor&&floorDetails&&!floorDetails.IsTransitioning)return;
            lastInteraction=Time.unscaledTime;
            SelectedFloor=desired;if(statusCard)statusCard.Hide();SelectedTopic=-1;
            zoomTarget=SelectedFloor==0?1:1.45f;
            float y=SelectedFloor==3?215.5f:SelectedFloor==2?-24.5f:-275;
            float limit=470.5f*(zoomTarget-1);
            cameraTarget=new Vector2(220*zoomTarget,(SelectedFloor==0?0:Mathf.Clamp(-y*zoomTarget,-limit,limit))-(buildingViewport?buildingViewport.anchoredPosition.y:0));
            if(floorDetails)floorDetails.OpenFloor(SelectedFloor);
            if(Run!=null&&!Run.StageEnded)feedback.text=SelectedFloor==0?"全楼视图 · 点击住户观察":"正在观察 "+SelectedFloor+" 楼";
            RefreshHud();
        }
        public void HighlightResident(int index)
        {
            for(int i=0;i<residents.Length;i++) { var outline=residents[i].GetComponent<Outline>();if(outline)outline.enabled=i==index; }
        }
        void ShowSettings() { CancelRecommendation();settingsPanel.SetActive(true); }
        public void TogglePause()
        {
            if(overlay.gameObject.activeSelf||Run.StageEnded)return;
            manualPause=!manualPause;if(manualPause)CancelRecommendation();RefreshHud();
        }
        public void CycleObservationSpeed()
        {
            if(!Run.Started||Run.StageEnded||overlay.gameObject.activeSelf||settingsPanel.activeSelf)return;
            paceIndex=(paceIndex+1)%3;lastInteraction=Time.unscaledTime;RefreshHud();
        }
        public void TickStage(float seconds)
        {
            bool ended=Run.StageEnded;if(!PausedForWorld)Run.Tick(seconds);
            ReadNewEvents();RefreshHud();
            if(!ended&&Run.StageEnded)PrepareSummary();
        }
        void PrepareSummary()
        {
            FrontendServices.Instance.CollectFinishedEnding();
            CancelRecommendation();RenderStates(Run.residents);summaryWaiting=(delivery&&delivery.IsBusy)||(catCare&&catCare.CareVisible);
            if(summaryWaiting)feedback.text="本阶段已结束 · 正在显示送达记录，顶部按钮可直接查看总结";else ShowSummary();
        }
        void ReadNewEvents()
        {
            while(lastEvent<Run.events.Count)
            {
                var e=Run.events[lastEvent++];feedback.text=e.text;lastInteraction=Time.unscaledTime;
                if(e.resident>=0)
                {
                    if(delivery&&(e.kind=="玩家推荐"||e.kind=="邻居分享"))delivery.Receive(e.resident,e.topic,e.source,e.after[e.resident].reaction,e.reaction);
                    else if(e.kind=="生活变化")ShowDeliveryReaction(e.resident,"添了「"+ResidentRun.TopicNames[e.topic]+"」用品",ResidentReaction.Quiet);
                    if(catCare)catCare.Queue(e);
                    if(e.source>=0)feedback.text=Run.SourceName(e.source)+" → "+e.text;
                    int floor=ResidentRun.FloorOf(e.resident);
                    if(changeNotice&&SelectedFloor!=0&&floor!=SelectedFloor)
                    {
                        noticeFloor=floor;changeNoticeLabel.text=floor+" 楼有新动态 →";changeNotice.gameObject.SetActive(true);
                    }
                }
                RenderStates(Run.residents);
            }
        }
        public void ShowDeliveryReaction(int resident,string response,ResidentReaction reaction)
        {
            if(reactionBubbles!=null&&resident<reactionBubbles.Length&&reactionBubbles[resident]) { reactionBubbles[resident].Show(response,reaction);return; }
            reactionLabels[resident].text=response;responseUntil[resident]=Time.unscaledTime+4.5f;
            reactionLabels[resident].color=reaction==ResidentReaction.Overloaded?new Color(.58f,.19f,.13f):reaction==ResidentReaction.Happy?new Color(.14f,.40f,.26f):new Color(.13f,.10f,.07f);
        }
        public void RenderStates(ResidentState[] states,int historicalStage=0)
        {
            if(statePropMotion)statePropMotion.CaptureStaticProps();
            for(int i=0;i<residents.Length;i++)
            {
                int topic=states[i].preferredTopic;Sprite still=activities[Activity[topic]];
                if(motions!=null&&i<motions.Length&&motions[i])motions[i].SetTopic(topic,still,i,states[i]);else residents[i].sprite=still;
                statusIcons[i].sprite=topicIcons[topic];residents[i].color=Color.white;
                recommendedLabels[i].text=states[i].recommended?"已推荐":"";
                for(int t=0;t<5;t++)
                {
                    propImages[i*5+t].gameObject.SetActive(states[i].propsUntil[t]>0&&states[i].propsUntil[t]>=(historicalStage>0?historicalStage:Run.Stage));
                    propImages[i*5+t].rectTransform.localScale=Vector3.one*(t==topic?1:.7f);
                }
                LayoutProps(i);
                if(catCare&&i==catCare.residentIndex)catCare.Apply(states[i],historicalStage>0?historicalStage:Run.Stage);
            }
            if(statePropMotion)statePropMotion.Apply(states);
        }
        void LayoutProps(int resident)
        {
            var visible=new List<Image>();
            for(int t=0;t<5;t++)if(propImages[resident*5+t].gameObject.activeSelf)visible.Add(propImages[resident*5+t]);
            for(int i=0;i<visible.Count;i++)
            {
                int row=visible.Count>3?i/3:0;int inRow=row==0?Mathf.Min(visible.Count,3):visible.Count-3;
                float x=((row==0?i:i-3)-(inRow-1)*.5f)*60;
                float y=visible.Count>3?(row==0?-51:-77):-76;
                if(residents[resident].rectTransform.anchoredPosition.y<-300)y=Mathf.Max(y,-48);
                visible[i].rectTransform.anchoredPosition=new Vector2(x,y);
            }
        }
        void RefreshHud()
        {
            if(Run==null)return;
            stageProgress.Refresh(Run,PausedForWorld,ObservationSpeed,WorldPauseReason);
            int selected=statusCard&&statusCard.gameObject.activeSelf?statusCard.SelectedResident:-1;
            int signature;
            unchecked { signature=Run.Stage;signature=signature*31+Run.Target;signature=signature*31+Run.Available;signature=signature*31+SelectedTopic;signature=signature*31+SelectedFloor;signature=signature*31+selected;signature=signature*31+paceIndex;
                signature=signature*31+(Run.Started?1:0);signature=signature*31+(Run.StageEnded?1:0);signature=signature*31+(manualPause?1:0);signature=signature*31+(focused?1:0);signature=signature*31+(overlay.gameObject.activeSelf?1:0);signature=signature*31+(settingsPanel.activeSelf?1:0);signature=signature*31+(Run.TutorialDone?1:0);signature=signature*31+(CanConfirm?1:0);signature=signature*31+(spreadStory&&spreadStory.IsActive?1:0); }
            if(signature==hudStamp)return;hudStamp=signature;HudRefreshCount++;
            bool recommended=selected>=0&&Run.residents[selected].recommended;
            bool canSelect=selected>=0&&!recommended&&!Run.StageEnded&&!manualPause&&!settingsPanel.activeSelf&&(Run.Started||!Run.TutorialDone);
            Color selection=UIFeedbackProfile.Default?UIFeedbackProfile.Default.selectionColor:new Color(1,.82f,.70f);
            for(int i=0;i<topics.Length;i++) { topics[i].interactable=canSelect;topics[i].GetComponent<Image>().color=i==SelectedTopic?selection:Color.white; }
            for(int i=0;i<floors.Length;i++)floors[i].GetComponent<Image>().color=i+1==SelectedFloor?selection:Color.white;
            allFloors.GetComponent<Image>().color=SelectedFloor==0?selection:Color.white;
            selectionLabel.text=selected<0?"先点击住户，再选主题":"住户 "+ResidentRun.RoomOf(selected)+" · "+(recommended?"本阶段已推荐":!Run.Started&&Run.TutorialDone?"先开始阶段":SelectedTopic<0?"选择推荐主题":"准备推荐："+ResidentRun.TopicNames[SelectedTopic]);
            if(targetLabel)targetLabel.text=Run.Target<0?"本局目标：尚未选择":"本局目标："+ResidentRun.TopicNames[Run.Target];
            if(pauseLabel)pauseLabel.text=manualPause?"继续":"暂停";
            pauseButton.interactable=Run.Started&&!Run.StageEnded&&!overlay.gameObject.activeSelf&&!settingsPanel.activeSelf;
            if(paceLabel)paceLabel.text="观察 ×"+ObservationSpeed;
            if(paceButton)paceButton.interactable=Run.Started&&!Run.StageEnded&&!manualPause&&!overlay.gameObject.activeSelf&&!settingsPanel.activeSelf;
            upFloor.interactable=SelectedFloor!=3;downFloor.interactable=SelectedFloor!=1;
            fastForward.interactable=Run.Started&&!Run.StageEnded&&!manualPause&&!overlay.gameObject.activeSelf&&!settingsPanel.activeSelf;
            bool watching=spreadStory&&spreadStory.IsActive;
            if(watching) { foreach(var button in topics.Concat(floors).Concat(statusIcons.Select(i=>i.GetComponent<Button>())).Concat(new[]{allFloors,upFloor,downFloor,pauseButton,fastForward,settingsButton,returnToMenu,paceButton,changeNotice,stageProgress.next}))if(button)button.interactable=false; }
            else { foreach(var button in floors.Concat(statusIcons.Select(i=>i.GetComponent<Button>())))button.interactable=true;allFloors.interactable=true;settingsButton.interactable=true;returnToMenu.interactable=true;if(changeNotice)changeNotice.interactable=true; }
            if(buildingTint&&!apartmentBackdrop)
            {
                Color night=BackgroundTimeOfDay=="晚上"?new Color(.09f,.12f,.32f,.33f):BackgroundTimeOfDay=="黄昏"?new Color(1,.46f,.25f,.16f):Color.clear;
                buildingTint.color=Color.Lerp(buildingTint.color,night,1-Mathf.Exp(-8*Time.unscaledDeltaTime));
            }
        }
        public void AdvanceStage()
        {
            if(!Run.Started) { if(Run.StartStage()) { overlay.Hide();feedback.text="第 1 阶段开始 · 每位住户一次直接推荐机会"; }RefreshHud();return; }
            if(Run.StageEnded)ShowSummary();
        }
        public void ContinueFromSummary()
        {
            if(Run.Finished) { ShowFinal();return; }
            if(!Run.NextStage())return;overlay.Hide();feedback.text="第 "+Run.Stage+" 阶段开始 · 上一阶段的兴趣和生活继续保留";RenderStates(Run.residents);ReadNewEvents();RefreshHud();
        }
        void AskFinishEarly()
        {
            if(!fastForward.interactable)return;
            CancelRecommendation();
            overlay.Show("提前结束本阶段？","还有 "+Run.Available+" 位住户没有收到你的直接推荐。\n\n剩余机会不会带入下一阶段。剩余时间的邻居分享与结算仍会正常执行，并显示分享方向。",new[]{"加速到结算","继续观察"},new Action[]{()=> { overlay.Hide();Run.FinishEarly();ReadNewEvents();PrepareSummary(); },()=>overlay.Hide()});
        }
        public void ShowSummary()
        {
            if(spreadStory)spreadStory.Stop(false);
            ClearReactionBubbles();
            FrontendServices.Instance.CollectFinishedEnding();
            replayLightingTime=-1;summaryWaiting=false;CancelRecommendation();
            if(delivery)delivery.Clear();
            replayPlaying=false;RenderStates(Run.residents);
            var entries=Run.events.Where(e=>e.stage==Run.Stage&&e.resident>=0).ToList();
            string text=Run.Reflection(Run.Stage)+"\n\n"+string.Join("\n",entries.Take(5).Select(e=>e.text))+"\n\n共 "+entries.Count+" 条变化记录，更多内容可在住户记录中查看。";
            overlay.Show("第 "+Run.Stage+" 阶段总结",text,new[]{"住户记录","回看本阶段",Run.Finished?"查看最终结果":"下一阶段","看看怎么传来的"},new Action[]{()=>ShowResidentJournal(0),()=>BeginReplay(false),ContinueFromSummary,BeginSpreadStory});
            overlay.buttons[3].interactable=spreadStory&&spreadStory.HasShare(Run.Stage);
        }
        public void BeginSpreadStory()
        {
            if(!spreadStory||!spreadStory.HasShare(Run.Stage))return;
            replayPlaying=false;if(catCare)catCare.Clear();if(delivery)delivery.Clear();
            spreadStory.Begin(Run.Stage,ShowSummary);RefreshHud();
        }
        public void ShowResidentLife(int index)
        {
            int prepared=SelectedTopic;
            CancelRecommendation();
            string wish=ResidentLifeStories.GetWish(index);
            overlay.Show(ResidentLifeStories.FocusLabel(index),"日常愿望："+wish+"\n\n现在："+ResidentLifeStories.CurrentLine(Run,index)+"\n\n"+
                "这只是他眼下的生活。下个阶段再回来看看，他还在做这件事吗？",new[]{"回到房间"},new Action[]{()=> { overlay.Hide();ShowResident(index);if(prepared>=0)SelectTopic(prepared); }});
        }
        void ShowResidentJournal(int index)
        {
            var r=Run.residents[index];var entries=Run.events.Where(e=>e.stage==Run.Stage&&e.resident==index).ToArray();
            overlay.Show("住户 "+ResidentRun.RoomOf(index)+" 的记录",(ResidentLifeStories.GetWish(index)==""?"":"日常愿望："+ResidentLifeStories.GetWish(index)+"\n"+ResidentLifeStories.CurrentLine(Run,index)+"\n\n")+"正在："+ActivityName(r.preferredTopic)+"\n\n"+(entries.Length==0?"本阶段没有新的接收记录。":string.Join("\n\n",entries.Select(e=>Run.SourceName(e.source)+" · "+e.text))),new[]{"上一位","下一位","返回总结"},new Action[]{()=>ShowResidentJournal((index+residents.Length-1)%residents.Length),()=>ShowResidentJournal((index+1)%residents.Length),ShowSummary});
        }
        public void BeginReplay(bool final)
        {
            if(spreadStory)spreadStory.Stop(false);if(catCare)catCare.Clear();
            ClearReactionBubbles();
            if(delivery)delivery.Clear();
            Array.Clear(responseUntil,0,responseUntil.Length);
            replayFinal=final;replayPlaying=false;replayPosition=0;replayClock=0;
            replayEvents=Run.events.Where(e=>final||e.stage==Run.Stage).ToList();ShowReplayEvent();
        }
        public void ReplayStep(int direction)
        {
            replayPlaying=false;replayClock=0;
            replayPosition=Mathf.Clamp(replayPosition+direction,0,Mathf.Max(0,replayEvents.Count-1));ShowReplayEvent();
        }
        void ToggleReplay()
        {
            if(!replayPlaying&&replayPosition==replayEvents.Count-1)replayPosition=0;
            replayPlaying=!replayPlaying;replayClock=0;ShowReplayEvent();
        }
        void ShowReplayEvent()
        {
            if(replayEvents.Count==0) { ShowSummary();return; }
            var e=replayEvents[replayPosition];replayLightingTime=e.tick*.1f;RenderStates(e.after,e.stage);
            if(e.resident>=0)SelectFloor(ResidentRun.FloorOf(e.resident));
            HighlightResident(e.resident);
            bool atEnd=replayPosition==replayEvents.Count-1;
            overlay.Show("回放 "+(replayPosition+1)+" / "+replayEvents.Count,"第 "+e.stage+" 阶段 · "+(e.tick*.1f).ToString("F1")+" 秒\n"+e.kind+" · "+Run.SourceName(e.source)+"\n\n"+e.text+"\n\n这是已发生的记录，回看不会发送新的推荐。",new[]{"上一事件",replayPlaying?"暂停回放":atEnd?"重播回放":"播放回放","下一事件",replayFinal?"返回最终结果":"返回总结"},new Action[]{()=>ReplayStep(-1),ToggleReplay,()=>ReplayStep(1),()=> { replayPlaying=false;RenderStates(Run.residents);if(replayFinal)ShowFinal();else ShowSummary(); }});
            overlay.buttons[0].interactable=replayPosition>0;
            overlay.buttons[1].interactable=replayEvents.Count>1;
            overlay.buttons[2].interactable=!atEnd;
            overlay.Layout(true);
        }
        public void ShowFinal()
        {
            if(!Run.Finished)return;
            var service=FrontendServices.Instance;var endingId=service.CollectFinishedEnding();var ending=endings.Get(endingId);
            replayLightingTime=-1;replayPlaying=false;HighlightResident(-1);RenderStates(Run.residents);var scores=Run.Scores();
            string ranking=string.Join("\n",Enumerable.Range(0,5).OrderByDescending(i=>scores[i]).Select(i=>ResidentRun.TopicNames[i]+"："+scores[i]+" 位住户愿意继续接触"));
            string achievement=(service.CurrentEndingWasNew?"本局新解锁成就：":"已解锁成就：")+ending.title+" · "+service.Achievements.Count+" / 6";
            string goal="本局目标："+ResidentRun.TopicNames[Run.Target]+" · "+(Run.TargetWon()?"目标达成":"目标未达成");
            overlay.Show("结局 · "+ending.title,ending.description+"\n\n"+achievement+"\n"+goal+"\n\n"+ranking+"\n\n"+ResidentLifeStories.EndingPathSummary(Run)+"\n\n同一个开局，换一种推法，会留下怎样的生活？",new[]{"关键回放","同开局再试","重新开始一局","返回标题"},new Action[]{()=>BeginReplay(true),()=>Restart(true),()=>Restart(false),()=>FrontendServices.Instance.LoadScene("MainMenu")});
            overlay.heading.color=ending.color;
        }
        public void RestartRun() { Restart(false); }
        public void Restart(bool sameOpening)
        {
            if(spreadStory)spreadStory.Stop(false);if(catCare)catCare.Clear();
            ClearReactionBubbles();hudStamp=int.MinValue;
            if(floorDetails)floorDetails.ReturnImmediatelyToBuilding();
            if(delivery)delivery.Clear();
            int seed=Run.seed;Run=sameOpening?FrontendServices.Instance.RetryRun(residents.Length,stageDuration,totalStages,seed,simulation):NewRun();
            replayLightingTime=-1;lastEvent=0;pendingTarget=-1;SelectedTopic=-1;manualPause=false;introStep=0;replayPlaying=false;summaryWaiting=false;paceIndex=0;
            if(changeNotice)changeNotice.gameObject.SetActive(false);
            Array.Clear(responseUntil,0,responseUntil.Length);statusCard.Hide();SelectFloor(0);RenderStates(Run.residents);ShowTarget();RefreshHud();
        }
        ResidentRun NewRun() { FrontendServices.Instance.RequestNewRun();return FrontendServices.Instance.EnsureRun(residents.Length,stageDuration,totalStages,simulation); }
        void ClearReactionBubbles() { if(reactionBubbles!=null)foreach(var bubble in reactionBubbles)if(bubble)bubble.Clear(); }
        void AskReturn()
        {
            CancelRecommendation();overlay.Show("结束本局并返回标题？","本局尚未结算的进度不会作为断点保留。",new[]{"返回标题","继续本局"},new Action[]{()=>FrontendServices.Instance.LoadScene("MainMenu"),()=> { overlay.Hide();if(Run.StageEnded)ShowSummary(); }});
        }
    }
}
