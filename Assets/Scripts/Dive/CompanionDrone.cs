using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Dive
{
    // 장착한 드론(없으면 drone_normal_01)이 주인을 따라 떠다니는 외형. 보트 씬(피셔 머리 왼쪽 위)과 잠수 씬(잠수부 왼쪽 위)에서 같이 쓴다.
    // 드론 효과(흡착 반경·자동 회수·위험물)는 GearBonuses·DiveDirector가 따로 다루고, 기본 외형은 효과가 없다.
    // 실행 중에 만들어지므로 Inspector 값은 플레이 중에 조절해 보고, 정한 값을 Create 호출부 기본값으로 옮긴다.
    public class CompanionDrone : MonoBehaviour
    {

        [Tooltip("따라갈 기준점에서 드론까지(월드 단위, 100px = 1). 기본은 왼쪽 위.")]
        [SerializeField] Vector2 offset = new Vector2(-0.45f, 0.15f);
        [Tooltip("위아래로 떠다니는 폭(월드 단위). 0.035 ≈ 3.5px")]
        [SerializeField] float bobAmplitude = 0.035f;
        [Tooltip("한 번 오르내리는 데 걸리는 시간(초)")]
        [SerializeField] float bobPeriod = 2f;
        [Tooltip("따라가는 지연(초). 0이면 기준점에 딱 붙어 움직인다.")]
        [SerializeField] float followSmoothTime;
        [Tooltip("128px 아이콘 크기 배율. 0.5 ≈ 64px")]
        [SerializeField] float scale = 0.5f;
        [SerializeField] int sortingOrder = 9;

        Transform _anchor;
        SpriteRenderer _renderer;
        SpriteRenderer _faceSource;
        Vector3 _velocity;
        float _phase;

        public static CompanionDrone Create(string name, Transform parent, Transform anchor, Vector2 offset, float followSmoothTime, int sortingOrder, SpriteRenderer faceSource = null)
        {
            if (anchor == null)
            {
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var drone = go.AddComponent<CompanionDrone>();
            drone._anchor = anchor;
            drone.offset = offset;
            drone.followSmoothTime = followSmoothTime;
            drone.sortingOrder = sortingOrder;
            drone._faceSource = faceSource;
            drone._renderer = go.AddComponent<SpriteRenderer>();
            drone._phase = Random.Range(0f, 1f);
            drone.RefreshSprite();
            go.transform.position = drone.Target();
            return drone;
        }

        // 장착이 바뀌면 다시 부른다.
        public void RefreshSprite()
        {
            if (_renderer != null)
            {
                _renderer.sprite = LoadSprite();
            }
        }

        public static Sprite LoadSprite()
        {
            return GearVisuals.EquippedWorldSprite(GearSlot.Drone);
        }

        // 타이틀·결과 화면은 timeScale이 0이라 흔들림과 따라가기는 unscaled 시간으로 돌린다.
        Vector3 Target()
        {
            var period = Mathf.Max(0.1f, bobPeriod);
            var bob = Mathf.Sin((Time.unscaledTime / period + _phase) * Mathf.PI * 2f) * bobAmplitude;
            return _anchor.position + new Vector3(offset.x, offset.y + bob, 0f);
        }

        void LateUpdate()
        {
            if (_anchor == null)
            {
                Destroy(gameObject);
                return;
            }

            // 부모(Nomad 0.8배 등)의 크기와 상관없이 화면에서 같은 크기로 보이게 한다.
            var parentScale = transform.parent != null ? Mathf.Max(0.01f, transform.parent.lossyScale.x) : 1f;
            transform.localScale = Vector3.one * (scale / parentScale);
            var target = Target();
            transform.position = followSmoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, target, ref _velocity, followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime)
                : target;

            // 드론 이미지는 오른쪽을 보고 있다. 주인이 왼쪽을 보면(flipX) 같이 뒤집는다.
            if (_faceSource != null)
            {
                _renderer.flipX = _faceSource.flipX;
                _renderer.sortingLayerID = _faceSource.sortingLayerID;
            }

            _renderer.sortingOrder = sortingOrder;
        }
    }
}
