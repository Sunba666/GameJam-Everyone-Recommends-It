using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class CharacterSceneWipe : MonoBehaviour
    {
        public Sprite[] characters;
        public float exitSeconds = 1.5f, enterSeconds = 1.2f;
        public Color paperColor = new Color(.984f, .945f, .863f, 1);
        public bool IsBusy { get; private set; }
        public string Phase { get; private set; } = "Idle";
        public float PhaseProgress { get; private set; }
        public int CharactersPerSide { get; private set; }
        public float LastExitDuration { get; private set; }
        public float LastEnterDuration { get; private set; }
        RectTransform _canvasRect, _diagonal;
        CanvasGroup _screen; RectTransform[] _heads, _tails;
        float _distance, _tailLength, _angle;
        Image _fallback;

        public void Begin(string scene)
        {
            if (IsBusy) return;
            if (!Application.CanStreamedLevelBeLoaded(scene)) { Debug.LogError("Scene is not in Build Settings: " + scene); return; }
            IsBusy = true; StartCoroutine(Change(scene));
        }
        void BuildCover()
        {
            if (_screen) Destroy(_screen.gameObject);
            var obj = new GameObject("PersistentTransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            obj.transform.SetParent(transform, false);
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = obj.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1672, 941); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvasRect = (RectTransform)obj.transform; _screen = obj.GetComponent<CanvasGroup>();
            _fallback = MakeImage("InputBlocker", _canvasRect, null, Color.clear);
            _fallback.rectTransform.anchorMin = Vector2.zero; _fallback.rectTransform.anchorMax = Vector2.one; _fallback.rectTransform.sizeDelta = Vector2.zero; _fallback.raycastTarget = true;
            Canvas.ForceUpdateCanvases();
            float w = _canvasRect.rect.width, h = _canvasRect.rect.height;
            _angle = -Mathf.Atan2(h, w) * Mathf.Rad2Deg;
            float diagonal = Mathf.Sqrt(w * w + h * h);
            // Project the entire viewport onto the axes of the rotated lanes.
            // Generous overscan keeps all four corners covered at non-16:9 ratios too.
            float across = 2 * w * h / diagonal + 320;
            const float laneWidth = 156;
            CharactersPerSide = Mathf.CeilToInt(across / laneWidth) + 1;
            _distance = diagonal * .5f + 320;
            _tailLength = diagonal + 640;
            _diagonal = new GameObject("OpposingCharacterLanes", typeof(RectTransform)).GetComponent<RectTransform>();
            _diagonal.SetParent(_canvasRect, false);
            _diagonal.anchorMin = _diagonal.anchorMax = new Vector2(.5f,.5f);
            _diagonal.anchoredPosition = Vector2.zero;
            _diagonal.localRotation = Quaternion.Euler(0, 0, _angle);
            _heads = new RectTransform[CharactersPerSide * 2]; _tails = new RectTransform[_heads.Length];
            for (int i = 0; i < CharactersPerSide; i++) for (int side = 0; side < 2; side++)
            {
                int index = i * 2 + side; float y = (i - (CharactersPerSide - 1) * .5f) * laneWidth;
                var tail = MakeImage("PaperTail_" + side + "_" + i, _diagonal, null, paperColor);
                tail.rectTransform.sizeDelta = new Vector2(_tailLength, laneWidth + 12); _tails[index] = tail.rectTransform;
                var head = MakeImage("Character_" + side + "_" + i, _diagonal, characters[i % characters.Length], Color.white);
                head.preserveAspect = true; head.rectTransform.sizeDelta = new Vector2(210, 210); head.rectTransform.localRotation = Quaternion.Euler(0, 0, -_angle);
                _heads[index] = head.rectTransform; _heads[index].anchoredPosition = new Vector2(0, y);
            }
            // Characters remain above all opaque tails, including neighboring lanes.
            foreach (var head in _heads) head.SetAsLastSibling();
            Position(-_distance);
        }
        Image MakeImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); g.transform.SetParent(parent, false);
            var img = g.GetComponent<Image>(); img.rectTransform.anchorMin=img.rectTransform.anchorMax=new Vector2(.5f,.5f);img.rectTransform.anchoredPosition=Vector2.zero;
            img.sprite = sprite; img.color = color; img.raycastTarget = false; return img;
        }
#if UNITY_EDITOR
        public bool CoveredByTails(Vector2 screenPoint)
        {
            if(_tails==null)return false;
            foreach(var tail in _tails)if(RectTransformUtility.RectangleContainsScreenPoint(tail,screenPoint,null))return true;
            return false;
        }
#endif
        void Position(float forward)
        {
            for (int i = 0; i < _heads.Length; i++)
            {
                float sign = i % 2 == 0 ? 1 : -1;
                float x = forward * sign; float y = _heads[i].anchoredPosition.y;
                _heads[i].anchoredPosition = new Vector2(x, y);
                _tails[i].anchoredPosition = new Vector2(x - sign * (_tailLength * .5f - 24), y);
            }
        }
        IEnumerator Change(string scene)
        {
            BuildCover(); bool motion = FrontendServices.Instance.settings.motion;
            _diagonal.gameObject.SetActive(motion);
            Phase = "Exit"; float begin = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - begin < exitSeconds)
            {
                PhaseProgress = Mathf.Clamp01((Time.realtimeSinceStartup - begin) / exitSeconds);
                if (motion) Position(Mathf.Lerp(-_distance, 0, PhaseProgress)); else _fallback.color = new Color(paperColor.r, paperColor.g, paperColor.b, PhaseProgress);
                yield return null;
            }
            LastExitDuration = Time.realtimeSinceStartup - begin; PhaseProgress = 1;
            if (motion) Position(0);
            // An opaque full-viewport backplate guards async loading and resize frames.
            _fallback.color = paperColor;
            Phase = "Loading"; yield return new WaitForEndOfFrame();
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            Phase = "Enter"; begin = Time.realtimeSinceStartup;
            if (motion) _fallback.color = Color.clear;
            while (Time.realtimeSinceStartup - begin < enterSeconds)
            {
                PhaseProgress = Mathf.Clamp01((Time.realtimeSinceStartup - begin) / enterSeconds);
                if (motion) Position(Mathf.Lerp(0, _distance + _tailLength + 240, PhaseProgress)); else _fallback.color = new Color(paperColor.r, paperColor.g, paperColor.b, 1 - PhaseProgress);
                yield return null;
            }
            LastEnterDuration = Time.realtimeSinceStartup - begin;
            Destroy(_screen.gameObject); _screen = null; Phase = "Idle"; PhaseProgress = 1; IsBusy = false;
        }
    }
}
