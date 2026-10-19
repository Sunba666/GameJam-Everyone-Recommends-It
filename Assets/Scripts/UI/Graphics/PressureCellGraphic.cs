using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // Flat coloured ink within the supplied bar outlines; no extra texture or inner bevel.
    public sealed class PressureCellGraphic : Image
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(fillAmount<=0)return;
            Rect r=GetPixelAdjustedRect();float right=Mathf.Lerp(r.xMin,r.xMax,fillAmount);
            mesh.AddVert(new Vector3(r.xMin,r.yMin),color,Vector2.zero);
            mesh.AddVert(new Vector3(r.xMin,r.yMax),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,r.yMax),color,Vector2.zero);
            mesh.AddVert(new Vector3(right,r.yMin),color,Vector2.zero);
            mesh.AddTriangle(0,1,2);mesh.AddTriangle(0,2,3);
        }
    }
}
