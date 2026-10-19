using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class ApartmentBackdrop : MonoBehaviour
    {
        public ResidentsView owner;
        public Image background;
        [Range(0,2)]public float lampStrength=1;
        public Material lightingMaterial;
        Material runtimeMaterial;
        public float NightBlend { get; private set; }
        public float DuskBlend { get; private set; }
        public Material ActiveMaterial=>runtimeMaterial;
        void Awake()
        {
            runtimeMaterial=new Material(lightingMaterial);background.material=runtimeMaterial;
        }
        void LateUpdate()
        {
            if(!owner||owner.Run==null||!runtimeMaterial)return;
            float speed=1-Mathf.Exp(-6*Time.unscaledDeltaTime);
            NightBlend=Mathf.Lerp(NightBlend,owner.BackgroundTimeOfDay=="晚上"?1:0,speed);
            DuskBlend=Mathf.Lerp(DuskBlend,owner.BackgroundTimeOfDay=="黄昏"?1:0,speed);
            runtimeMaterial.SetFloat("_Night",NightBlend);runtimeMaterial.SetFloat("_Dusk",DuskBlend);runtimeMaterial.SetFloat("_LampStrength",lampStrength);
            // Faces stay readable in the lit rooms instead of receiving an outdoor darkness overlay.
            foreach(var actor in owner.residents)actor.color=Color.Lerp(Color.white,new Color(.98f,.89f,.73f),NightBlend*.48f);
        }
        void OnDestroy() { if(runtimeMaterial)Destroy(runtimeMaterial); }
    }
}
