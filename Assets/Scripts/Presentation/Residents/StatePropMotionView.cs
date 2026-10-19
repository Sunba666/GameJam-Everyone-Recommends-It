using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // Activity companions are presentation only. Life-object history remains in ResidentRun.
    public sealed class StatePropMotionView : MonoBehaviour
    {
        public ResidentsView owner;
        public Image[] companions;
        public Animator[] companionAnimators;
        // Sitting cat, lying cat, floating notes. Only their Image sprite is animated.
        public RuntimeAnimatorController[] controllers;
        public Sprite[] stills;
        public Animator careAnimator;
        public Vector2 lyingPosition=new Vector2(-75,-24),lyingSize=new Vector2(118,74);
        public Vector2 notesPosition=new Vector2(-72,55),notesSize=new Vector2(92,92);
        ResidentState[] displayed;
        bool[] originalCatEnabled,originalNotesEnabled;
        public int DisplayedCount=>displayed==null?0:displayed.Length;
        public int DisplayedTopic(int index)=>displayed!=null&&index>=0&&index<displayed.Length?displayed[index].preferredTopic:-1;

        void OnEnable() { FrontendServices.SettingsChanged+=Refresh;Refresh(); }
        void OnDisable()
        {
            FrontendServices.SettingsChanged-=Refresh;RestoreStaticProps();
            if(companionAnimators!=null)foreach(var animator in companionAnimators)if(animator)animator.enabled=false;
            if(companions!=null)foreach(var image in companions)if(image)image.gameObject.SetActive(false);
            if(careAnimator)careAnimator.enabled=false;
        }
        public void Apply(ResidentState[] states)
        {
            displayed=states;CaptureStaticProps();Refresh();
        }
        public void CaptureStaticProps()
        {
            if(originalCatEnabled!=null||!owner||owner.propImages==null)return;
            originalCatEnabled=new bool[owner.residents.Length];originalNotesEnabled=new bool[owner.residents.Length];
            for(int index=0;index<owner.residents.Length;index++)
            {
                originalCatEnabled[index]=owner.propImages[index*5].enabled;
                if(owner.catCare&&owner.catCare.residentIndex==index&&owner.catCare.CareVisible&&owner.catCare.originalCatProp==owner.propImages[index*5])
                    originalCatEnabled[index]=true;
                originalNotesEnabled[index]=owner.propImages[index*5+4].enabled;
            }
        }
        bool Available(int kind)=>controllers!=null&&kind>=0&&kind<controllers.Length&&controllers[kind]&&stills!=null&&kind<stills.Length&&stills[kind];
        void Refresh()
        {
            if(!owner||owner.Run==null||companions==null||companionAnimators==null)return;
            CaptureStaticProps();bool motion=FrontendServices.Instance&&FrontendServices.Instance.settings.motion;
            bool paused=owner.PausedForWorld;
            for(int index=0;index<companions.Length;index++)
            {
                int topic=DisplayedTopic(index),kind=topic==0?1:topic==4?2:-1;
                bool care=owner.catCare&&owner.catCare.residentIndex==index&&owner.catCare.CareVisible;
                bool visible=kind>=0&&Available(kind)&&!care;
                var image=companions[index];if(!image||index>=companionAnimators.Length)continue;
                image.color=owner.residents[index].color;
                if(kind>=0)
                {
                    image.rectTransform.anchoredPosition=kind==1?lyingPosition:notesPosition;
                    image.rectTransform.sizeDelta=kind==1?lyingSize:notesSize;
                }
                Configure(image,companionAnimators[index],kind,index,visible,motion,paused);
                if(originalCatEnabled!=null&&index<originalCatEnabled.Length)
                {
                    owner.propImages[index*5].enabled=originalCatEnabled[index]&&!(topic==0&&Available(1))&&!care;
                    owner.propImages[index*5+4].enabled=originalNotesEnabled[index]&&!(topic==4&&Available(2));
                }
            }
            if(owner.catCare&&owner.catCare.cat&&careAnimator)
            {
                if(Available(0))
                {
                    bool care=owner.catCare.CareVisible&&!owner.catCare.IsSwiping;
                    owner.catCare.cat.color=owner.residents[owner.catCare.residentIndex].color;
                    Configure(owner.catCare.cat,careAnimator,0,owner.catCare.residentIndex,care,motion,paused);
                }
                else careAnimator.enabled=false;
            }
        }
        void Configure(Image image,Animator animator,int kind,int index,bool visible,bool motion,bool paused)
        {
            if(!animator)return;
            // FloorDetailView reparents the same actor through an inactive viewport.
            // Keep its phase through that transient disable instead of flashing frame0.
            animator.keepAnimatorStateOnDisable=true;
            if(!visible)
            {
                animator.enabled=false;image.gameObject.SetActive(false);return;
            }
            var controller=controllers[kind];
            if(animator.runtimeAnimatorController!=controller) { animator.enabled=false;animator.runtimeAnimatorController=controller; }
            image.gameObject.SetActive(true);animator.speed=paused?0:1;
            if(!image.gameObject.activeInHierarchy) { animator.enabled=false;image.sprite=stills[kind];return; }
            if(!motion) { animator.enabled=false;image.sprite=stills[kind];return; }
            if(!animator.enabled)
            {
                // Start the notes in view, including when the opening/tutorial freezes
                // their clock. Preserve the native entry/exit once the stage starts.
                animator.enabled=true;animator.Rebind();animator.Play(0,0,(index*.137f+(kind==2?.24f:0))%1);animator.Update(0);
            }
        }
        void RestoreStaticProps()
        {
            if(!owner||originalCatEnabled==null)return;
            for(int index=0;index<originalCatEnabled.Length;index++)
            {
                bool care=owner.catCare&&owner.catCare.residentIndex==index&&owner.catCare.CareVisible;
                if(owner.propImages[index*5])owner.propImages[index*5].enabled=originalCatEnabled[index]&&!care;
                if(owner.propImages[index*5+4])owner.propImages[index*5+4].enabled=originalNotesEnabled[index];
            }
        }
        void Update()=>Refresh();
        // CatCare updates its visible pose during Update. Apply companions afterwards too,
        // so its swiping frame cannot expose a second cat or an old static note image.
        void LateUpdate()=>Refresh();
    }
}
