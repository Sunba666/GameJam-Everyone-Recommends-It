using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // The supplied card already provides the circular outline; draw its changing expression.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttitudeFaceGraphic : MaskableGraphic
    {
        int _attitude;
        public int Attitude { get => _attitude; set { _attitude = value; SetVerticesDirty(); } }
        static readonly Color Ink = new Color(.09f, .08f, .1f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            Vector2 center = rectTransform.rect.center; float line = size * .075f;
            if (_attitude == 2)
            {
                Line(mesh, center + new Vector2(-.28f,.2f)*size, center + new Vector2(-.12f,.06f)*size, line, Ink);
                Line(mesh, center + new Vector2(.28f,.2f)*size, center + new Vector2(.12f,.06f)*size, line, Ink);
            }
            else
            {
                Dot(mesh, center + new Vector2(-.21f,.12f)*size, size*.065f, Ink);
                Dot(mesh, center + new Vector2(.21f,.12f)*size, size*.065f, Ink);
            }
            if (_attitude == 1) Line(mesh, center + new Vector2(-.16f,-.18f)*size, center + new Vector2(.16f,-.18f)*size, line, Ink);
            else
            {
                float start = _attitude == 0 ? 200 : 25, end = _attitude == 0 ? 340 : 155;
                Vector2 mouth = center + new Vector2(0, _attitude == 0 ? .02f : -.29f)*size;
                for (int i = 0; i < 12; i++)
                {
                    float a = Mathf.Lerp(start,end,i/12f)*Mathf.Deg2Rad, b = Mathf.Lerp(start,end,(i+1)/12f)*Mathf.Deg2Rad;
                    Line(mesh, mouth + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size*.23f,
                        mouth + new Vector2(Mathf.Cos(b),Mathf.Sin(b))*size*.23f, line, Ink);
                }
                if (_attitude == 0)
                {
                    var pink = new Color(1,.64f,.56f);
                    Dot(mesh, center + new Vector2(-.32f,-.08f)*size, size*.07f, pink);
                    Dot(mesh, center + new Vector2(.32f,-.08f)*size, size*.07f, pink);
                }
            }
        }
        static void Dot(VertexHelper mesh, Vector2 center, float radius, Color color)
        {
            int first=mesh.currentVertCount; mesh.AddVert(center,color,Vector2.zero);
            for(int i=0;i<=16;i++)
            {
                float a=i*Mathf.PI*2/16; mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
                if(i>0)mesh.AddTriangle(first,first+i,first+i+1);
            }
        }
        static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            int first=mesh.currentVertCount;
            mesh.AddVert(a-normal,color,Vector2.zero); mesh.AddVert(a+normal,color,Vector2.zero);
            mesh.AddVert(b+normal,color,Vector2.zero); mesh.AddVert(b-normal,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2); mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
