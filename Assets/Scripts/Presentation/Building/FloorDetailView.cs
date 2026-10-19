using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    [DefaultExecutionOrder(-20)]
    public sealed class FloorDetailView : MonoBehaviour
    {
        public ResidentsView owner;
        public FloorSceneDefinition[] definitions;
        public GameObject panel;
        public CanvasGroup group;
        public RectTransform viewport,stage;
        public Image paper,dayImage,nightImage;
        public Text caption;
        public GameObject[] anchorGroups;
        public FloorInteractionAnchor[] anchors;
        public float residentScale=1.4f;
        public bool IsOpen { get; private set; }
        public bool IsTransitioning { get; private set; }
        public int CurrentFloor { get; private set; }
        public float NightWeight { get; private set; }
        public FloorSceneDefinition CurrentDefinition=>IsOpen?definitions[CurrentFloor-1]:null;
        Vector2[] originalPositions,originalSizes;
        Vector3[] originalScales;
        int requestedFloor,pendingResident=-1;
        float transitionTime;
        bool committed;
        void Awake() { SaveOriginals();panel.SetActive(false); }
        void SaveOriginals()
        {
            if(originalPositions!=null)return;
            originalPositions=owner.residents.Select(r=>r.rectTransform.anchoredPosition).ToArray();
            originalSizes=owner.residents.Select(r=>r.rectTransform.sizeDelta).ToArray();
            originalScales=owner.residents.Select(r=>r.rectTransform.localScale).ToArray();
        }
        public void OpenFloor(int floor)
        {
            SaveOriginals();floor=Mathf.Clamp(floor,0,3);
            if(!IsTransitioning&&floor==CurrentFloor)return;
            requestedFloor=floor;pendingResident=-1;transitionTime=0;committed=false;IsTransitioning=true;
            if(!FrontendServices.Instance.settings.motion) { Commit();group.alpha=1;FinishTransition(); }
        }
        public void QueueResident(int resident) { pendingResident=resident; }
        public FloorInteractionAnchor FindAnchor(string id)=>anchors.FirstOrDefault(a=>a&&a.stableId==id);
        void RestoreActors()
        {
            for(int i=0;i<owner.residents.Length;i++)
            {
                var actor=owner.residents[i].rectTransform;actor.SetParent(owner.buildingContent,false);
                actor.anchoredPosition=originalPositions[i];actor.sizeDelta=originalSizes[i];actor.localScale=originalScales[i];
                actor.gameObject.SetActive(true);
            }
        }
        void Commit()
        {
            RestoreActors();CurrentFloor=requestedFloor;IsOpen=CurrentFloor>0;
            owner.buildingViewport.gameObject.SetActive(!IsOpen);
            foreach(var anchorGroup in anchorGroups)anchorGroup.SetActive(false);
            if(!IsOpen) { panel.SetActive(false);return; }
            var definition=definitions[CurrentFloor-1];panel.SetActive(true);
            dayImage.sprite=definition.day;nightImage.sprite=definition.night;anchorGroups[CurrentFloor-1].SetActive(true);
            caption.text=CurrentFloor+"F · 室内详细观察 · "+definition.Room(0)+" / "+definition.Room(1)+" / "+definition.Room(2);
            NightWeight=owner.BackgroundTimeOfDay=="晚上"?1:owner.BackgroundTimeOfDay=="黄昏" ? .35f : 0;
            for(int i=0;i<owner.residents.Length;i++)
            {
                bool visible=ResidentRun.FloorOf(i)==CurrentFloor;owner.residents[i].gameObject.SetActive(visible);
                if(!visible)continue;
                int slot=i%3;var actor=owner.residents[i].rectTransform;actor.SetParent(stage,false);
                actor.anchoredPosition=Vector2.Scale(definition.residents[slot]-Vector2.one*.5f,stage.rect.size);
                actor.localScale=originalScales[i]*residentScale;
            }
            if(owner.statusCard.gameObject.activeSelf)owner.statusCard.Reposition();
        }
        void FinishTransition()
        {
            IsTransitioning=false;committed=true;stage.localScale=Vector3.one;
            int queued=pendingResident;pendingResident=-1;
            if(queued>=0&&ResidentRun.FloorOf(queued)==CurrentFloor)owner.ShowResident(queued);
        }
        void Update()
        {
            if(IsTransitioning)
            {
                if(!FrontendServices.Instance.settings.motion) { if(!committed)Commit();group.alpha=1;FinishTransition();return; }
                var profile=UIFeedbackProfile.Default;float fadeOut=profile?profile.floorFadeOut:.22f,fadeIn=profile?profile.floorFadeIn:.22f;
                transitionTime+=Time.unscaledDeltaTime;
                if(!committed&&transitionTime>=fadeOut) { Commit();committed=true; }
                if(IsOpen)
                {
                    float settle=Mathf.SmoothStep(0,1,(transitionTime-fadeOut)/fadeIn);
                    group.alpha=committed?settle:Mathf.Clamp01(1-transitionTime/fadeOut);
                    if(committed)stage.localScale=Vector3.one*Mathf.Lerp(profile?profile.floorEntryScale:1.05f,1,settle);
                }
                if(transitionTime>=fadeOut+fadeIn)FinishTransition();
            }
        }
        void LateUpdate()
        {
            if(!IsOpen||owner.Run==null)return;
            float desired=owner.BackgroundTimeOfDay=="晚上"?1:owner.BackgroundTimeOfDay=="黄昏" ? .35f : 0;
            NightWeight=Mathf.Lerp(NightWeight,desired,1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            nightImage.color=new Color(1,1,1,NightWeight);
            paper.color=Color.Lerp(new Color(.94f,.89f,.80f),new Color(.20f,.16f,.12f),NightWeight);
            caption.color=Color.Lerp(new Color(.20f,.15f,.10f),new Color(1,.93f,.81f),NightWeight);
            foreach(var actor in owner.residents)if(actor.gameObject.activeInHierarchy)actor.color=Color.Lerp(Color.white,new Color(1,.93f,.82f),NightWeight*.3f);
        }
        public void ReturnImmediatelyToBuilding()
        {
            requestedFloor=0;pendingResident=-1;Commit();IsTransitioning=false;group.alpha=1;stage.localScale=Vector3.one;
        }
    }
}
