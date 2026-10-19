using System;
using UnityEngine;

namespace EveryoneRecommends.Frontend
{
    // Only six bits of local progress. Settings and run seeds use separate storage.
    public sealed class EndingAchievements
    {
        public const string SaveKey="EveryoneRecommends.EndingAchievements.v1";
        readonly string key;
        int unlocked;
        public int Count
        {
            get { int count=0;for(int i=0;i<6;i++)if(IsUnlocked((EndingId)i))count++;return count; }
        }
        public EndingAchievements(string storageKey=SaveKey)
        {
            if(string.IsNullOrEmpty(storageKey))throw new ArgumentException("Storage key is required.",nameof(storageKey));
            key=storageKey;unlocked=PlayerPrefs.GetInt(key,0)&63;
        }
        public bool IsUnlocked(EndingId id)=>(int)id>=0&&(int)id<6&&(unlocked&(1<<(int)id))!=0;
        public bool Unlock(EndingId id)
        {
            if((int)id<0||(int)id>=6||IsUnlocked(id))return false;
            unlocked|=1<<(int)id;PlayerPrefs.SetInt(key,unlocked);PlayerPrefs.Save();return true;
        }
    }
}
