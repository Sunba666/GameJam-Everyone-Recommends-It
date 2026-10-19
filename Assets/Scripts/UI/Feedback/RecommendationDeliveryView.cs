using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class RecommendationDeliveryView : MonoBehaviour
    {
        public ResidentsView owner;
        public DeliveryArrowGraphic arrow;
        public DeliveryArrowGraphic worldArrow;
        public Text routeLabel;
        public Image[] receiptIcons;
        public Text[] receiptLabels;
        public RectTransform[] receiptBubbles;
        public float travelSeconds=.65f,holdSeconds=.35f,receiptSeconds=3.4f;
        public Color[] themeColors={new Color(1,.40f,.28f),new Color(.95f,.62f,.12f),new Color(.32f,.48f,.86f),new Color(.19f,.65f,.59f),new Color(.62f,.37f,.79f)};
        struct Delivery { public int resident,topic,source;public string response;public ResidentReaction reaction; }
        readonly Queue<Delivery> pending=new Queue<Delivery>();
        float[] receiptUntil,receiptStarted;
        Delivery current;
        float elapsed;
        bool active;
        public int DeliveryCount { get; private set; }
        public int LastResident { get; private set; }=-1;
        public int LastTopic { get; private set; }=-1;
        public int CurrentSource { get; private set; }=-1;
        public bool IsBusy=>active||pending.Count>0;
        public bool ArrowVisible=>arrow&&arrow.gameObject.activeSelf;
        void Awake() { receiptUntil=new float[receiptIcons.Length];receiptStarted=new float[receiptIcons.Length];Clear(); }
        public void Receive(int resident,int topic,int source,string response=null,ResidentReaction reaction=ResidentReaction.Quiet)
        {
            if(resident<0||resident>=receiptIcons.Length||topic<0||topic>=owner.topicIcons.Length)return;
            if(receiptUntil==null) { receiptUntil=new float[receiptIcons.Length];receiptStarted=new float[receiptIcons.Length]; }
            DeliveryCount++;LastResident=resident;LastTopic=topic;
            if(source<0)FrontendServices.Instance.PlayRecommendation(topic,false);
            pending.Enqueue(new Delivery{resident=resident,topic=topic,source=source,response=response,reaction=reaction});
        }
        void Update()
        {
            if(owner.overlay.gameObject.activeSelf||owner.EffectsPaused)
            {
                for(int i=0;i<receiptUntil.Length;i++)if(receiptUntil[i]>0) { receiptUntil[i]+=Time.unscaledDeltaTime;receiptStarted[i]+=Time.unscaledDeltaTime; }
                arrow.gameObject.SetActive(false);if(worldArrow)worldArrow.gameObject.SetActive(false);if(routeLabel)routeLabel.gameObject.SetActive(false);return;
            }
            bool motion=FrontendServices.Instance.settings.motion;
            for(int i=0;i<receiptIcons.Length;i++)
            {
                float remaining=receiptUntil[i]-Time.unscaledTime;
                SetReceiptVisible(i,remaining>0);
                float age=Time.unscaledTime-receiptStarted[i];
                float size=motion?Mathf.Lerp(.55f,1,Mathf.SmoothStep(0,1,age/.22f)):1;
                ReceiptRoot(i).localScale=Vector3.one*size;
            }
            if(!active&&pending.Count>0)
            {
                current=pending.Dequeue();active=true;elapsed=0;CurrentSource=current.source;
                receiptIcons[current.resident].sprite=owner.topicIcons[current.topic];receiptLabels[current.resident].text=current.source<0?"收到推荐":"邻居分享";
                receiptIcons[current.resident].color=Color.white;receiptStarted[current.resident]=Time.unscaledTime;
                receiptUntil[current.resident]=Time.unscaledTime+receiptSeconds;SetReceiptVisible(current.resident,true);
                ReceiptRoot(current.resident).localScale=Vector3.one*(motion?.55f:1);
                if(current.response!=null)owner.ShowDeliveryReaction(current.resident,current.response,current.reaction);
                if(current.source>=0)FrontendServices.Instance.PlayRecommendation(current.topic,true);
                if(routeLabel)routeLabel.text=current.source<0?"你 → "+ResidentRun.RoomOf(current.resident)+" · "+ResidentRun.TopicNames[current.topic]:
                    ResidentRun.RoomOf(current.source)+" → "+ResidentRun.RoomOf(current.resident)+" · "+ResidentRun.TopicNames[current.topic];
            }
            if(!active)return;
            elapsed+=Time.unscaledDeltaTime;
            var area=arrow.rectTransform;
            var source=current.source<0?(RectTransform)owner.topics[current.topic].transform:owner.statusIcons[current.source].rectTransform;
            Vector3 startWorld=source.TransformPoint(new Vector3(0,current.source<0?source.rect.yMin+10:source.rect.yMax,0));
            var destination=owner.statusIcons[current.resident].rectTransform;
            Vector3 endWorld=destination.TransformPoint(new Vector3(0,destination.rect.yMin+5,0));
            float travel=current.source>=0 ? .5f : travelSeconds,hold=current.source>=0 ? .25f : holdSeconds;
            Color tint=themeColors[current.topic];
            tint.a=motion?Mathf.Clamp01((travel+hold-elapsed)/.18f):1;
            arrow.gameObject.SetActive(true);
            arrow.Draw(area.InverseTransformPoint(startWorld),area.InverseTransformPoint(endWorld),motion?elapsed/travel:1,tint);
            if(routeLabel)
            {
                routeLabel.gameObject.SetActive(true);
                routeLabel.rectTransform.anchoredPosition=new Vector2(620,destination.anchoredPosition.y+57);
                routeLabel.color=new Color(.18f,.14f,.11f,tint.a);
            }
            if(worldArrow)
            {
                bool visible=current.source>=0&&(owner.SelectedFloor==0||owner.SelectedFloor==ResidentRun.FloorOf(current.resident));
                worldArrow.gameObject.SetActive(visible);
                if(visible)
                {
                    var from=owner.residents[current.source].rectTransform;var to=owner.residents[current.resident].rectTransform;
                    float direction=Mathf.Sign(to.position.x-from.position.x);
                    var origin=worldArrow.rectTransform.InverseTransformPoint(from.TransformPoint(new Vector3(direction*54,65,0)));
                    var target=worldArrow.rectTransform.InverseTransformPoint(to.TransformPoint(new Vector3(-direction*54,65,0)));
                    worldArrow.Draw(origin,target,motion?elapsed/travel:1,tint,false,22);
                }
            }
            if(elapsed>=travel+hold)HideArrow();
        }
        void HideArrow() { active=false;if(arrow)arrow.gameObject.SetActive(false);if(worldArrow)worldArrow.gameObject.SetActive(false);if(routeLabel)routeLabel.gameObject.SetActive(false); }
        RectTransform ReceiptRoot(int index)=>receiptBubbles!=null&&index<receiptBubbles.Length&&receiptBubbles[index]?receiptBubbles[index]:receiptIcons[index].rectTransform;
        void SetReceiptVisible(int index,bool visible)
        {
            ReceiptRoot(index).gameObject.SetActive(visible);receiptIcons[index].gameObject.SetActive(visible);
        }
        public void Clear()
        {
            pending.Clear();HideArrow();
            if(receiptUntil!=null)System.Array.Clear(receiptUntil,0,receiptUntil.Length);
            for(int i=0;i<receiptIcons.Length;i++)if(receiptIcons[i]) { SetReceiptVisible(i,false);ReceiptRoot(i).localScale=Vector3.one;receiptIcons[i].rectTransform.localScale=Vector3.one; }
        }
    }
}
