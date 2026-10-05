using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace EveryoneRecommends.Frontend
{
    public sealed class MenuView : MonoBehaviour
    {
        public Button start, howTo, settings, achievements, quit;
        public GameObject howToPanel, settingsPanel, achievementsPanel;
        public Button howToClose, howToBack, previousPage, nextPage;
        public Text pageTitle, pageBody, pageNumber;
        public string gameScene = "Residents";
        public int CurrentPage { get; private set; }
        static readonly string[] Titles = { "你只是点了自己想看的", "看看他们怎样改变", "选择会留下痕迹" };
        static readonly string[] Bodies = {
            "猫、美食、游戏、新闻、音乐。\n\n选择你想看的内容，留意住户收到推送后的反应。\n有人感兴趣，有人保持原样。",
            "观察住户的表情、活动和房间。\n\n喜欢一种内容，不代表永远想收到同样的推送。\n切换楼层，也看看镜头外发生了什么。",
            "上一阶段的选择，会延续到下一阶段。\n\n阶段结束后查看总结，回看自己的选择与影响。\n设置中的音量、全屏与动态效果会自动保存。"
        };
        void Awake()
        {
            start.onClick.AddListener(StartGame); howTo.onClick.AddListener(ShowHowTo); settings.onClick.AddListener(ShowSettings); achievements.onClick.AddListener(ShowAchievements);
            if (quit) quit.onClick.AddListener(ExitGame);
            howToClose.onClick.AddListener(ClosePanels); howToBack.onClick.AddListener(ClosePanels);
            previousPage.onClick.AddListener(() => SetPage(CurrentPage - 1)); nextPage.onClick.AddListener(() => SetPage(CurrentPage + 1));
            ClosePanels();
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && !FrontendServices.Instance.IsTransitioning) ClosePanels();
        }
        public void StartGame() { if (FrontendServices.Instance.IsTransitioning) return; ClosePanels(); FrontendServices.Instance.LoadScene(gameScene); }
        public void ClosePanels()
        {
            howToPanel.SetActive(false); settingsPanel.SetActive(false); achievementsPanel.SetActive(false);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        public void ShowHowTo()
        {
            ClosePanels();
            howToPanel.transform.Find("Heading").GetComponent<Text>().text = "怎么玩";
            previousPage.gameObject.SetActive(true); nextPage.gameObject.SetActive(true); pageNumber.gameObject.SetActive(true);
            SetPage(0); howToPanel.SetActive(true);
        }
        public void ShowSettings() { ClosePanels(); settingsPanel.SetActive(true); }
        public void ShowAchievements() { ClosePanels(); achievementsPanel.SetActive(true); }
        public void ExitGame()
        {
            if (FrontendServices.Instance.IsTransitioning) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            // Browsers own their tabs; show a closeable notice instead of a nonfunctional quit call.
            ClosePanels();
            howToPanel.transform.Find("Heading").GetComponent<Text>().text = "退出游戏";
            pageTitle.text = "请关闭当前浏览器标签页";
            pageBody.text = "网页游戏由浏览器运行。\n\n关闭这个标签页即可退出。\n点击返回，可以继续留在主界面。";
            previousPage.gameObject.SetActive(false); nextPage.gameObject.SetActive(false); pageNumber.gameObject.SetActive(false);
            howToPanel.SetActive(true);
#else
            Application.Quit();
#endif
        }
        public void SetPage(int n)
        {
            CurrentPage = Mathf.Clamp(n, 0, Titles.Length - 1);
            pageTitle.text = Titles[CurrentPage]; pageBody.text = Bodies[CurrentPage]; pageNumber.text = (CurrentPage + 1) + " / " + Titles.Length;
            previousPage.interactable = CurrentPage > 0; nextPage.interactable = CurrentPage < Titles.Length - 1;
        }
    }
}
