using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Dive
{
    // 장착한 헬멧(없으면 helmet_normal_01)을 잠수부 머리에 씌운다. 잠수부의 자식이라 같이 움직이고, flipX도 따라간다.
    // 잠수부 애니메이션은 프레임마다 머리 위치가 달라서, 80x80 프레임 기준 머리 중심 표를 보고 매 프레임 옮긴다.
    // 대기는 서 있는 자세(머리가 위), 수영은 누운 자세(머리가 오른쪽 끝)이고, 피격 중에는 몸이 굴러서 헬멧을 숨긴다.
    public class HelmetSkin : MonoBehaviour
    {
        const float FrameSize = 80f;
        const float PixelsPerUnit = 100f;

        static readonly Vector2[] IdleHead =
        {
            new Vector2(45.5f, 19f), new Vector2(45.5f, 20f), new Vector2(45.5f, 21f),
            new Vector2(45.5f, 22f), new Vector2(45.5f, 21f), new Vector2(45.5f, 20f)
        };

        static readonly Vector2 SwimHead = new Vector2(58f, 34f);

        [Tooltip("128px 헬멧 이미지 배율. 0.14 ≈ 머리(약 10px)를 덮는 13px")]
        [SerializeField] float scale = 0.14f;
        [Tooltip("머리 중심에서 헬멧까지(프레임 픽셀, 오른쪽·위가 +)")]
        [SerializeField] Vector2 headOffsetPx = new Vector2(0f, 1f);

        SpriteRenderer _diver;
        SpriteRenderer _helmet;

        public static HelmetSkin Attach(GameObject diver)
        {
            if (diver == null)
            {
                return null;
            }

            var skin = diver.GetComponentInChildren<HelmetSkin>(true);
            if (skin != null)
            {
                return skin;
            }

            var go = new GameObject("Helmet");
            go.transform.SetParent(diver.transform, false);
            skin = go.AddComponent<HelmetSkin>();
            skin._diver = diver.GetComponent<SpriteRenderer>();
            skin._helmet = go.AddComponent<SpriteRenderer>();
            skin.Refresh();
            return skin;
        }

        // 잠수를 시작할 때마다 불러 장착 변경을 반영한다.
        public void Refresh()
        {
            if (_helmet != null)
            {
                _helmet.sprite = GearVisuals.EquippedWorldSprite(GearSlot.Helmet);
            }
        }

        void LateUpdate()
        {
            if (_diver == null || _helmet == null)
            {
                return;
            }

            var visible = TryHead(_diver.sprite, out var head);
            _helmet.enabled = visible && _diver.enabled;
            if (!visible)
            {
                return;
            }

            // 잠수부 스프라이트는 피벗 중앙이다. flipX면 가로를 뒤집는다(헬멧 이미지는 오른쪽을 본다).
            var x = (head.x + headOffsetPx.x - FrameSize * 0.5f) / PixelsPerUnit;
            var y = (FrameSize * 0.5f - head.y + headOffsetPx.y) / PixelsPerUnit;
            transform.localPosition = new Vector3(_diver.flipX ? -x : x, y, 0f);
            transform.localScale = Vector3.one * scale;
            _helmet.flipX = _diver.flipX;
            _helmet.color = _diver.color;
            _helmet.sortingLayerID = _diver.sortingLayerID;
            _helmet.sortingOrder = _diver.sortingOrder + 1;
        }

        // 스프라이트 이름은 SpriteSheet.Slice가 붙인 "{텍스처}_{프레임}"이다.
        public static bool TryHead(Sprite sprite, out Vector2 head)
        {
            head = default;
            if (sprite == null)
            {
                return false;
            }

            var name = sprite.name;
            var cut = name.LastIndexOf('_');
            var frame = cut >= 0 && int.TryParse(name.Substring(cut + 1), out var parsed) ? parsed : 0;
            if (name.Contains("idle"))
            {
                head = IdleHead[Mathf.Clamp(frame, 0, IdleHead.Length - 1)];
                return true;
            }

            if (name.Contains("swim"))
            {
                head = SwimHead;
                return true;
            }

            return false;
        }
    }
}
