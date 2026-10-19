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
            "先选本局目标，再点住户 → 选主题 → 确认推荐。\n\n只选主题不会发送。教学推荐计入第1阶段；\n点击顶部“开始”后才计时，每人每阶段一次机会。",
            "人物在做什么，表示当前活动；\n头顶收到什么，表示刚接收的内容，两者可以不同。\n\n喜欢一种内容，也可能暂时不想再刷。\n点住户可暂停观察，切楼层仍会继续计时。",
            "每阶段90秒，共12阶段。机会不必全部用完。\n\n用“观察 ×2 / ×4”减少等待；提前结算仍会处理邻居分享。\n阶段结束后回看真实记录，再用同开局验证新猜想。"
        };
        void Awake()
        {
            // These fixed-size help buttons were authored as stretched artwork.
            // Slicing their large imported border collapses the center over the label.
            foreach(var button in new[]{previousPage,nextPage,howToBack,howToClose})
                if(button&&button.targetGraphic is Image plate)plate.type=Image.Type.Simple;
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
        public void StartGame() { if (FrontendServices.Instance.IsTransitioning) return; ClosePanels(); FrontendServices.Instance.RequestNewRun(); FrontendServices.Instance.LoadScene(gameScene); }
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
            SetPage(0); howToPanel.transform.SetAsLastSibling();howToPanel.SetActive(true);
        }
        public void ShowSettings() { ClosePanels();settingsPanel.transform.SetAsLastSibling();settingsPanel.SetActive(true); }
        public void ShowAchievements() { ClosePanels();achievementsPanel.transform.SetAsLastSibling();achievementsPanel.SetActive(true); }
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
