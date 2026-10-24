using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Dive
{
    // 장착한 드론이 잠수부 옆을 따라다니는 외형. 효과(흡착 반경·자동 회수·위험물)는 DiveDirector가 따로 처리한다.
    // 드론 이미지는 오른쪽을 보고 있어서, 왼쪽으로 갈 때는 flipX로 뒤집는다.
    public class DroneBuddy : MonoBehaviour
    {
        // 128 아이콘(PPU 100)을 화면에서 약 64px로 줄인다.
        const float Scale = 0.5f;
        const float FollowSharpness = 5f;
        const float BobHeight = 0.08f;
        const float BobSpeed = 2.6f;
        static readonly Vector2 Offset = new Vector2(0.62f, 0.48f);

        Transform _diver;
        SpriteRenderer _renderer;
        SpriteRenderer _diverRenderer;
        float _facing = 1f;
        float _lastDiverX;

        public static DroneBuddy Spawn(Transform parent, Transform diver)
        {
            var drone = GearBonuses.Equipped(GearSlot.Drone);
            if (drone == null || drone.Icon == null || diver == null)
            {
                return null;
            }

            var go = new GameObject("DroneBuddy");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * Scale;
            var buddy = go.AddComponent<DroneBuddy>();
            buddy._diver = diver;
            buddy._diverRenderer = diver.GetComponent<SpriteRenderer>();
            buddy._renderer = go.AddComponent<SpriteRenderer>();
            buddy._renderer.sprite = drone.Icon;
            buddy._lastDiverX = diver.position.x;
            go.transform.position = buddy.Target();
            return buddy;
        }

        Vector3 Target()
        {
            var bob = Mathf.Sin(Time.time * BobSpeed) * BobHeight;
            return _diver.position + new Vector3(-Offset.x * _facing, Offset.y + bob, 0f);
        }

        void LateUpdate()
        {
            if (_diver == null)
            {
                Destroy(gameObject);
                return;
            }

            var dx = _diver.position.x - _lastDiverX;
            _lastDiverX = _diver.position.x;
            if (Mathf.Abs(dx) > 0.002f)
            {
                _facing = dx > 0f ? 1f : -1f;
            }

            var t = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, Target(), t);
            _renderer.flipX = _facing < 0f;
            if (_diverRenderer != null)
            {
                _renderer.sortingLayerID = _diverRenderer.sortingLayerID;
                _renderer.sortingOrder = _diverRenderer.sortingOrder + 1;
            }
        }
    }
}
