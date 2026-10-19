using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // UI geometry avoids importing another texture and remains sharp at any Canvas scale.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DeliveryArrowGraphic : MaskableGraphic
    {
        Vector2 start,end;
        float progress;
        bool showHalo=true;
        float arcHeight;
        public void Draw(Vector2 from,Vector2 to,float amount,Color tint,bool receiverHalo=true,float bendHeight=0)
        {
            start=from;end=to;progress=Mathf.Clamp01(amount);color=tint;showHalo=receiverHalo;arcHeight=bendHeight;SetVerticesDirty();
        }
        Vector2 Point(float t)
        {
            // A small bend keeps the upward direction clear without crossing the panel edge.
            Vector2 middle=(start+end)*.5f+new Vector2(Mathf.Clamp((end.x-start.x)*.12f,-28,28),arcHeight);
            return (1-t)*(1-t)*start+2*(1-t)*t*middle+t*t*end;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(progress<=0)return;
            const int segments=32;const float halfWidth=4;
            Vector2 previous=Point(0),direction=Vector2.up;
            for(int i=1;i<=segments;i++)
            {
                Vector2 next=Point(progress*i/segments);direction=(next-previous).normalized;
                Vector2 normal=new Vector2(-direction.y,direction.x)*halfWidth;
                int index=mesh.currentVertCount;
                mesh.AddVert(previous-normal,color,Vector2.zero);mesh.AddVert(previous+normal,color,Vector2.zero);
                mesh.AddVert(next+normal,color,Vector2.zero);mesh.AddVert(next-normal,color,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);previous=next;
            }
            Vector2 side=new Vector2(-direction.y,direction.x)*12;
            int head=mesh.currentVertCount;
            mesh.AddVert(previous+direction*7,color,Vector2.zero);
            mesh.AddVert(previous-direction*14+side,color,Vector2.zero);
            mesh.AddVert(previous-direction*14-side,color,Vector2.zero);
            mesh.AddTriangle(head,head+1,head+2);
            if(!showHalo)return;
            // Mark the receiver immediately, even while the traveling head passes other nodes.
            Vector2 center=end+Vector2.up*29;float radius=41+Mathf.Sin(progress*Mathf.PI)*2;
            Color halo=color;halo.a*=.75f;
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                Vector2 first=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),next=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int index=mesh.currentVertCount;
                mesh.AddVert(center+first*(radius-2),halo,Vector2.zero);mesh.AddVert(center+first*radius,halo,Vector2.zero);
                mesh.AddVert(center+next*radius,halo,Vector2.zero);mesh.AddVert(center+next*(radius-2),halo,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
