using System;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // IDs are save-data keys. Keep their values stable when editing names or artwork.
    public enum EndingId { None=-1, Cat=0, Food=1, Game=2, News=3, Music=4, Balanced=5 }
    [Serializable]
    public sealed class EndingDefinition
    {
        public EndingId id;
        public string title;
        [TextArea(2,4)] public string description;
        public Color color=Color.black;
        public string Condition=>id==EndingId.Balanced?"最终结算时，最高人数并列":
            "最终结算时，"+ResidentRun.TopicNames[(int)id]+"独占第一";
    }
    [CreateAssetMenu(menuName="Everyone Recommends/Six Endings",fileName="EndingCatalog")]
    public sealed class EndingCatalog : ScriptableObject
    {
        public EndingDefinition[] endings;
        public Sprite[] topicIcons;
        public EndingDefinition Get(EndingId id)=>Array.Find(endings,e=>e.id==id);
    }
}
