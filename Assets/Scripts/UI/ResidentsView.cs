using UnityEngine;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    // A functional scene hand-off: floor selection, topic preview, settings and return.
    // The full stage simulation can replace SelectTopic without changing menu/transition code.
    public sealed class ResidentsView : MonoBehaviour
    {
        public Button[] topics, floors;
        public Button allFloors, returnToMenu, settingsButton;
        public GameObject settingsPanel;
        public Image[] residents, statusIcons;
        public Sprite[] activities, topicIcons;
        public Text selectionLabel, feedback;
        public int SelectedFloor { get; private set; }
        public int SelectedTopic { get; private set; } = -1;
        int _nextResident;
        static readonly string[] Names = { "猫", "美食", "游戏", "新闻", "音乐" };
        static readonly int[] Activity = { 2, 4, 3, 1, 0 };
        void Awake()
        {
            for (int i = 0; i < topics.Length; i++) { int n = i; topics[i].onClick.AddListener(() => SelectTopic(n)); }
            for (int i = 0; i < floors.Length; i++) { int floor = i + 1; floors[i].onClick.AddListener(() => SelectFloor(floor)); }
            allFloors.onClick.AddListener(() => SelectFloor(0));
            returnToMenu.onClick.AddListener(() => FrontendServices.Instance.LoadScene("MainMenu"));
            settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            settingsPanel.SetActive(false); SelectFloor(0);
        }
        void Update() { if (Input.GetKeyDown(KeyCode.Escape) && !FrontendServices.Instance.IsTransitioning) settingsPanel.SetActive(!settingsPanel.activeSelf); }
        public void SelectFloor(int floor)
        {
            SelectedFloor = floor;
            for (int i = 0; i < residents.Length; i++) residents[i].color = new Color(1, 1, 1, floor == 0 || FloorOf(i) == floor ? 1 : .3f);
            feedback.text = floor == 0 ? "全楼视图 · 选择今天想看的内容" : "正在观察 " + floor + " 楼";
        }
        static int FloorOf(int i) { return i < 3 ? 3 : i < 6 ? 2 : 1; }
        public void SelectTopic(int topic)
        {
            SelectedTopic = topic; selectionLabel.text = "今天想看：" + Names[topic];
            for (int i = 0; i < topics.Length; i++) topics[i].GetComponent<Image>().color = i == topic ? new Color(1, .78f, .7f) : Color.white;
            int chosen = _nextResident++ % residents.Length;
            if (SelectedFloor != 0) for (int i = 0; i < residents.Length; i++) if (FloorOf((chosen + i) % residents.Length) == SelectedFloor) { chosen = (chosen + i) % residents.Length; break; }
            residents[chosen].sprite = activities[Activity[topic]];
            statusIcons[chosen].sprite = topicIcons[topic];
            feedback.text = FloorOf(chosen) + " 楼的住户看到了「" + Names[topic] + "」";
        }
    }
}
