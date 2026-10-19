using System;
using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // One paper panel for introduction, stage journal, read-only replay and ending.
    public sealed class DemoOverlay : MonoBehaviour
    {
        public Text heading,body;
        public RectTransform paper;
        public Image scrim;
        public Button[] buttons;
        public Text[] labels;
        public ScrollRect reading;
        public RectTransform readingViewport;
        public Text scrollHint;
        int actionCount;
        public void Show(string title,string text,string[] names,Action[] actions)
        {
            actionCount=names.Length;
            heading.color=new Color(.14f,.10f,.07f);
            heading.text=title;body.text=text;
            for(int i=0;i<buttons.Length;i++)
            {
                buttons[i].onClick.RemoveAllListeners();bool visible=i<names.Length;
                buttons[i].gameObject.SetActive(visible);
                buttons[i].interactable=true;
                if(visible) { labels[i].text=names[i];int index=i;buttons[i].onClick.AddListener(()=>actions[index]()); }
            }
            Layout(false);
            if(reading)reading.verticalNormalizedPosition=1;
            gameObject.SetActive(true);
        }
        public void Hide() { gameObject.SetActive(false); }
        public void Layout(bool replay)
        {
            if(!paper)return;
            paper.anchoredPosition=replay?new Vector2(620,0):Vector2.zero;
            paper.sizeDelta=replay?new Vector2(410,650):new Vector2(920,690);
            paper.GetComponent<Image>().type=replay?Image.Type.Simple:Image.Type.Sliced;
            heading.rectTransform.anchoredPosition=new Vector2(0,replay?264:282);heading.rectTransform.sizeDelta=new Vector2(replay?338:808,58);heading.fontSize=replay?26:36;
            heading.resizeTextMaxSize=replay?26:36;
            bool twoRows=replay?actionCount>2:actionCount>4;
            float width=replay?326:808,height=replay?310:twoRows?360:380;
            body.fontSize=replay?18:22;body.horizontalOverflow=HorizontalWrapMode.Wrap;body.verticalOverflow=VerticalWrapMode.Overflow;
            if(readingViewport)
            {
                readingViewport.anchoredPosition=new Vector2(0,replay?70:twoRows?55:45);readingViewport.sizeDelta=new Vector2(width,height);
                body.rectTransform.anchorMin=body.rectTransform.anchorMax=new Vector2(.5f,1);
                body.rectTransform.pivot=new Vector2(.5f,1);body.rectTransform.anchoredPosition=Vector2.zero;
                body.rectTransform.sizeDelta=new Vector2(width,height);
                float contentHeight=Mathf.Max(height,body.preferredHeight+8);body.rectTransform.sizeDelta=new Vector2(width,contentHeight);
                reading.vertical=contentHeight>height+1;reading.StopMovement();
                if(scrollHint) { scrollHint.text=reading.vertical?"滚轮查看完整记录 ↓":"";scrollHint.rectTransform.anchoredPosition=new Vector2(0,replay?-116:twoRows?-146:-169);scrollHint.rectTransform.sizeDelta=new Vector2(width,22); }
            }
            else { body.rectTransform.anchoredPosition=new Vector2(0,replay?50:45);body.rectTransform.sizeDelta=new Vector2(width,height); }
            if(scrim)scrim.color=new Color(0,0,0,replay?.04f:.28f);
            int columns=replay?2:Mathf.Min(actionCount,actionCount>4?3:4);
            if(actionCount>6&&!replay)columns=4;
            columns=Mathf.Max(1,columns);float gap=replay?14:20;
            float buttonWidth=replay?156:Mathf.Min(280,(808-(columns-1)*gap)/columns);
            for(int i=0;i<buttons.Length;i++)
            {
                var rect=(RectTransform)buttons[i].transform;
                int row=i/columns,inRow=Mathf.Min(columns,actionCount-row*columns);
                rect.anchoredPosition=new Vector2(((i%columns)-(inRow-1)*.5f)*(buttonWidth+gap),(replay?-183:twoRows?-196:-218)-row*62);
                rect.sizeDelta=new Vector2(buttonWidth,52);
                labels[i].rectTransform.anchoredPosition=Vector2.zero;labels[i].rectTransform.sizeDelta=rect.sizeDelta-new Vector2(24,12);
                labels[i].alignment=TextAnchor.MiddleCenter;labels[i].fontSize=replay?20:23;
                labels[i].resizeTextForBestFit=true;labels[i].resizeTextMinSize=18;labels[i].resizeTextMaxSize=replay?20:23;
                labels[i].horizontalOverflow=HorizontalWrapMode.Wrap;labels[i].verticalOverflow=VerticalWrapMode.Truncate;
            }
        }
    }
}
