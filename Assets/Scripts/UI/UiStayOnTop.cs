using UnityEngine;

namespace ScrapFishing.UI
{
    // 형제 중 맨 마지막(맨 앞)에 그려지게 유지한다. 오리파·결과 화면이 SetAsLastSibling으로 올라와도 다시 그 위로 간다.
    public class UiStayOnTop : MonoBehaviour
    {
        void LateUpdate()
        {
            var parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1)
            {
                transform.SetAsLastSibling();
            }
        }
    }
}
