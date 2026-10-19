using System;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    public enum FloorAnchorKind { Seat, Tabletop, Floor, Wall, Window }
    [Serializable]
    public sealed class FloorAnchorSpec
    {
        [Range(0,2)]public int residentInFloor;
        public FloorAnchorKind kind;
        public Vector2 normalizedPosition;
        public Vector2 normalizedSize=new Vector2(.06f,.06f);
    }
    [CreateAssetMenu(menuName="Everyone Recommends/Floor Interior",fileName="FloorInterior")]
    public sealed class FloorSceneDefinition : ScriptableObject
    {
        [Range(1,3)]public int floor;
        public Sprite day,night;
        public Vector2[] residents={new Vector2(.23f,.42f),new Vector2(.50f,.42f),new Vector2(.77f,.42f)};
        public FloorAnchorSpec[] anchors;
        public string Room(int index)=>(floor*100+index+1).ToString();
    }
}
