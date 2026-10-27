using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ScrapFishing.UI
{
    // 튜토리얼 딤·카드가 받은 터치를 게임(TapReader·SwipeReader·SteerReader)으로 흘리지 않는다.
    // PointerUtil.IsOverUi가 IPointerClickHandler를 "누를 수 있는 UI"로 보므로, 붙이기만 해도 아래 게임 입력이 막힌다.
    public class TutorialTapCatcher : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }
    }
}
