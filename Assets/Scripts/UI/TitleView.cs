using System;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    public class TitleView : MonoBehaviour
    {
        [SerializeField] Button startButton;

        Action _onStart;

        public void Bind(Action onStart)
        {
            _onStart = onStart;
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStart);
                startButton.onClick.AddListener(HandleStart);
            }
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        void HandleStart()
        {
            _onStart?.Invoke();
        }
    }
}
