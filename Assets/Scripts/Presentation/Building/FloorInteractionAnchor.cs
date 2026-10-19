using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // A stable placement socket, not a clickable placeholder or a second simulation object.
    [RequireComponent(typeof(RectTransform))]
    public sealed class FloorInteractionAnchor : MonoBehaviour
    {
        public string stableId;
        public string room;
        public FloorAnchorKind kind;
        public RectTransform Rect=>(RectTransform)transform;
#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var corners=new Vector3[4];Rect.GetWorldCorners(corners);Gizmos.color=new Color(.15f,.75f,.45f);
            for(int i=0;i<4;i++)Gizmos.DrawLine(corners[i],corners[(i+1)%4]);
            UnityEditor.Handles.Label(corners[1],stableId);
        }
#endif
    }
}
