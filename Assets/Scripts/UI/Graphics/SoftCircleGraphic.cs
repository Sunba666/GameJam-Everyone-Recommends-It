using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // A small procedural UI mesh: no extra bitmap or shader dependency.
    public sealed class SoftCircleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; var center = r.center;
            const int count = 64;
            vh.AddVert(center, color, new Vector2(.5f, .5f));
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i <= count; i++)
            {
                float a = i * Mathf.PI * 2 / count; float radius = ring == 0 ? .8f : 1;
                var c = color; if (ring == 1) c.a = 0;
                vh.AddVert(center + new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f) * radius, c, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                vh.AddTriangle(0, i + 1, i + 2);
                int a = i + 1, b = count + 2 + i;
                vh.AddTriangle(a, b, b + 1); vh.AddTriangle(a, b + 1, a + 1);
            }
        }
    }
}
