using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    [CreateAssetMenu(menuName="Everyone Recommends/UI Feedback",fileName="Default")]
    public sealed class UIFeedbackProfile : ScriptableObject
    {
        [Header("Button contents; hit areas remain fixed")]
        [Range(1,1.12f)] public float hoverScale=1.055f;
        [Range(.85f,1)] public float pressedScale=.94f;
        [Min(.03f)] public float buttonResponse=.10f;
        [Header("Resident reaction")]
        [Min(1)] public float reactionSeconds=4.5f;
        [Min(.03f)] public float reactionEnter=.14f,reactionExit=.25f;
        [Header("Floor camera")]
        [Min(.05f)] public float floorFadeOut=.22f,floorFadeIn=.22f;
        [Range(1,1.12f)] public float floorEntryScale=1.05f;
        [Min(1)] public float cameraResponse=8;
        [Header("Readable observation card")]
        public Vector2 cardSize=new Vector2(300,400);
        [Range(16,22)] public int cardFontSize=17;
        [Range(10,40)] public float placementStep=20;
        public Color selectionColor=new Color(1,.82f,.70f);
        static UIFeedbackProfile cached;
        public static UIFeedbackProfile Default=>cached?cached:cached=Resources.Load<UIFeedbackProfile>("UIFeedback/Default");
    }
}
