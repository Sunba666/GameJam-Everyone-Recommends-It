using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class AchievementsView : MonoBehaviour
    {
        public EndingCatalog catalog;
        public AchievementCardView[] cards;
        public Text progress,details;
        int selection=-1;
        void Awake()
        {
            for(int i=0;i<cards.Length;i++) { int index=i;cards[i].button.onClick.AddListener(()=>Select(index)); }
        }
        void OnEnable() { Refresh(); }
        public void Refresh()
        {
            if(!FrontendServices.Instance)return;
            var store=FrontendServices.Instance.Achievements;
            foreach(var card in cards)card.Refresh(catalog,store);
            progress.text="已解锁 "+store.Count+" / 6 · 每个结局对应一个成就";
            if(selection>=0)Select(selection);else details.text="点击卡片查看条件 · 第 12 阶段最终结算后解锁，重开与退出后保留。";
        }
        public void Select(int index)
        {
            selection=index;var card=cards[index];var ending=catalog.Get(card.id);
            foreach(var candidate in cards)candidate.selected.enabled=candidate==card;
            details.text=ending.title+" · "+ending.Condition+"\n"+(card.IsUnlocked?ending.description:"尚未达成。按最终实际结果解锁，与你选定的目标是否达成分别计算。");
        }
    }
}
