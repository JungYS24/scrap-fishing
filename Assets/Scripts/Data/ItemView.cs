using UnityEngine;

namespace ScrapFishing.Data
{
    // ItemDataImporter가 만드는 아이템 프리팹(Assets/Prefabs/Items)용. 게임 화면은 이 프리팹을 쓰지 않고
    // ScrapView + ItemData.Frames(SpriteAnimator)로 그린다. Animator 연결은 프리팹을 에디터에서 볼 때만 쓰인다.
    public class ItemView : MonoBehaviour
    {
        [SerializeField] ItemData data;

        public ItemData Data => data;

        public void Apply(ItemData item)
        {
            data = item;
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null && item != null)
            {
                renderer.sprite = item.Icon;
            }

            if (item != null && item.HasAnimation)
            {
                var animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = gameObject.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = item.AnimatorController;
            }
        }
    }
}
