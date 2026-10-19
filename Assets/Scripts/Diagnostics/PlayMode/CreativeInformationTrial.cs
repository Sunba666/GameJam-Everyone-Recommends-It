#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // Repeatable short, natural-action scene for human information-design comparison.
    // Editor-only: the ordinary Windows run and achievement save format are unchanged.
    public sealed class CreativeInformationTrial : MonoBehaviour
    {
        EndingAchievements original;
        FrontendServices service;
        string key;
        IEnumerator Start()
        {
            service=FrontendServices.Instance;key="EveryoneRecommends.InformationTrial."+Guid.NewGuid().ToString("N");
            original=service.ReplaceAchievementStoreForTest(new EndingAchievements(key));
            var run=service.RetryRun(9,90,12,14,new SimulationParameters());
            run.SetTarget(0);run.Recommend(0,0);run.StartStage();
            for(int stage=2;stage<=4;stage++) { run.FinishEarly();run.NextStage();if(stage<4)run.Recommend(0,0); }
            service.LoadScene("Residents");while(service.IsTransitioning)yield return null;
            var view=FindObjectOfType<ResidentsView>();view.SelectFloor(3);
            while(view.floorDetails.IsTransitioning)yield return null;
            // The earlier history is real but not presented again as a fresh delivery.
            float sfx=service.settings.sfx;service.settings.sfx=0;service.Apply(false);
            view.TickStage(0);view.delivery.Clear();view.catCare.Clear();
            service.settings.sfx=sfx;service.Apply(false);
            run.Recommend(0,0);view.TickStage(0);
            view.feedback.text="先观察这一刻，再点击 301。信息按钮可以切换两种显示。";
        }
        void OnDestroy()
        {
            if(!service||original==null)return;
            service.ReplaceAchievementStoreForTest(original);PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();original=null;
            service.RequestNewRun();
        }
    }
}
#endif
