using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ScrapFishing.UI
{
    // 슬라이더처럼 누르기를 받는 오브젝트에 붙여, 손을 뗀 순간을 알려 준다.
    public class UiPointerRelease : MonoBehaviour, IPointerUpHandler
    {
        public event Action Released;

        public void OnPointerUp(PointerEventData eventData)
        {
            Released?.Invoke();
        }
    }
}
