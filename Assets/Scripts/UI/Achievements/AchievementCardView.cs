using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class AchievementCardView : MonoBehaviour
    {
        public EndingId id;
        public Button button;
        public Image icon;
        public Image[] balanceIcons;
        public Text title,condition,status;
        public Outline selected;
        public bool IsUnlocked { get; private set; }
        public void Refresh(EndingCatalog catalog,EndingAchievements progress)
        {
            var ending=catalog.Get(id);IsUnlocked=progress.IsUnlocked(id);
            title.text=ending.title;condition.text=ending.Condition;status.text=IsUnlocked?"已解锁":"未解锁";
            title.color=IsUnlocked?ending.color:new Color(.35f,.31f,.26f);
            status.color=IsUnlocked?new Color(.18f,.46f,.31f):new Color(.52f,.47f,.40f);
            icon.gameObject.SetActive(id!=EndingId.Balanced);
            if(id!=EndingId.Balanced) { icon.sprite=catalog.topicIcons[(int)id];icon.color=new Color(1,1,1,IsUnlocked?1:.38f); }
            for(int i=0;i<balanceIcons.Length;i++)
            {
                balanceIcons[i].gameObject.SetActive(id==EndingId.Balanced);balanceIcons[i].sprite=catalog.topicIcons[i];
                balanceIcons[i].color=new Color(1,1,1,IsUnlocked?1:.38f);
            }
        }
    }
}
