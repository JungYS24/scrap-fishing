using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // 9-slice 스프라이트를 칸 높이에 맞춰 원본과 같은 비율로 그린다.
    // 원본 높이 / 칸 높이를 pixelsPerUnitMultiplier로 써서, 칸이 원본보다 작으면 테두리도 같이 얇아진다.
    [RequireComponent(typeof(Image))]
    public class SlicedFit : UIBehaviour
    {
        Image _image;

        public static void Apply(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = Color.white;
            var fit = image.GetComponent<SlicedFit>();
            if (fit == null)
            {
                fit = image.gameObject.AddComponent<SlicedFit>();
            }

            fit.Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Refresh();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            var height = ((RectTransform)transform).rect.height;
            if (_image == null || _image.sprite == null || height <= 1f)
            {
                return;
            }

            var multiplier = Mathf.Max(0.05f, _image.sprite.rect.height / height);
            if (!Mathf.Approximately(_image.pixelsPerUnitMultiplier, multiplier))
            {
                _image.pixelsPerUnitMultiplier = multiplier;
            }
        }
    }
}
