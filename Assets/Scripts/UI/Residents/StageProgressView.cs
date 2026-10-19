using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class StageProgressView : MonoBehaviour
    {
        public Image fill;
        public RectTransform cursor;
        public Text stageLabel,timeLabel,hint,nextLabel;
        public Button next;
        public ResidentsView owner;
        public float trackWidth=736;
        int lastSecond=-1,lastStage=-1,lastAvailable=-1;
        float lastSpeed=-1;
        string lastDay,lastPauseReason;
        bool lastPaused,lastEnded,lastStarted;
        void Awake() { next.onClick.AddListener(() => owner.AdvanceStage()); }
        public void Refresh(ResidentRun run, bool paused,float speed=1,string pauseReason="已暂停")
        {
            float fraction=Mathf.Clamp01(run.Remaining/run.stageDuration);
            fill.fillAmount=fraction;
            cursor.anchoredPosition=new Vector2((fraction-.5f)*trackWidth,0);
            cursor.gameObject.SetActive(fraction>0);
            fill.color=fraction<1f/6 ? new Color(1,.42f,.32f) : Color.white;
            if(lastStage!=run.Stage) { lastStage=run.Stage;stageLabel.text="阶段 "+run.Stage+" / "+run.totalStages; }
            int second=Mathf.CeilToInt(run.Remaining);if(lastSecond!=second) { lastSecond=second;timeLabel.text=second+" 秒";timeLabel.color=second<=15&&run.Started&&!run.StageEnded?new Color(.62f,.23f,.17f):new Color(.14f,.10f,.07f); }
            next.gameObject.SetActive(run.StageEnded||!run.Started);
            next.interactable=!(owner&&owner.spreadStory&&owner.spreadStory.IsActive)&&(run.StageEnded||run.Target>=0&&run.TutorialDone);
            string nextText=run.Finished?"最终阶段总结":run.StageEnded?"阶段总结":"开始第1阶段";if(nextLabel.text!=nextText)nextLabel.text=nextText;
            hint.gameObject.SetActive(run.Started&&!run.StageEnded);
            string day=run.TimeOfDay;
            if(lastPaused!=paused||lastPauseReason!=pauseReason||lastDay!=day||lastAvailable!=run.Available||lastSpeed!=speed||lastEnded!=run.StageEnded||lastStarted!=run.Started)
            {
                lastPaused=paused;lastPauseReason=pauseReason;lastDay=day;lastAvailable=run.Available;lastSpeed=speed;lastEnded=run.StageEnded;lastStarted=run.Started;
                hint.text=paused?pauseReason:day+" · 还可推荐 "+run.Available+" 次 · ×"+speed;
            }
        }
    }
}
