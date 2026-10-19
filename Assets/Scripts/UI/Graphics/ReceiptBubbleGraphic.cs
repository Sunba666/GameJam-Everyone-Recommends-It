using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // Paper speech balloon with a short tail toward its resident. No bitmap dependency.
    public sealed class ReceiptBubbleGraphic : MaskableGraphic
    {
        public Color paper=new Color(1,.967f,.885f);
        public Color ink=new Color(.14f,.11f,.09f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var bounds=rectTransform.rect;
            var body=new Rect(bounds.xMin,bounds.yMin+8,bounds.width,bounds.height-8);
            Triangle(vh,new Vector2(body.xMin+12,body.yMin+5),new Vector2(body.xMin+5,bounds.yMin-2),new Vector2(body.xMin+32,body.yMin+5),ink);
            Rounded(vh,body,11,ink);
            Rounded(vh,new Rect(body.x+2,body.y+2,body.width-4,body.height-4),9,paper);
            Triangle(vh,new Vector2(body.xMin+14,body.yMin+6),new Vector2(body.xMin+9,bounds.yMin+2),new Vector2(body.xMin+28,body.yMin+6),paper);
        }
        static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color color)
        {
            int i=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);
        }
        static void Rounded(VertexHelper vh,Rect rect,float radius,Color color)
        {
            int center=vh.currentVertCount;vh.AddVert(rect.center,color,Vector2.zero);
            const int steps=8;int count=4*(steps+1);
            for(int corner=0;corner<4;corner++)
            {
                var origin=new Vector2(corner==0||corner==3?rect.xMax-radius:rect.xMin+radius,corner<2?rect.yMax-radius:rect.yMin+radius);
                for(int i=0;i<=steps;i++)
                {
                    float angle=(corner*90+i*90f/steps)*Mathf.Deg2Rad;
                    vh.AddVert(origin+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)vh.AddTriangle(center,center+1+i,center+1+(i+1)%count);
        }
    }
}
