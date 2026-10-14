using UnityEngine;

namespace ScrapFishing.Data
{
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
