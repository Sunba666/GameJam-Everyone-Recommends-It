using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EveryoneRecommends.Frontend
{
    public sealed class UiClickSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        void Play() { var s = GetComponent<Selectable>(); if ((!s || s.IsInteractable()) && FrontendServices.Instance) FrontendServices.Instance.Click(); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Play(); }
        public void OnSubmit(BaseEventData e) { Play(); }
    }
}
