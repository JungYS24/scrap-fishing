using ScrapFishing.Core;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class HookMover : MonoBehaviour
    {
        [SerializeField] Transform rodTip;
        [SerializeField] Transform lineEnd;

        public Vector3 Position => transform.position;

        LineRenderer _line;

        public void Build(Vector3 surfacePoint)
        {
            transform.position = surfacePoint;

            _line = gameObject.GetComponent<LineRenderer>();
            if (_line == null)
            {
                _line = gameObject.AddComponent<LineRenderer>();
            }

            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.startWidth = 0.035f;
            _line.endWidth = 0.02f;
            _line.sortingOrder = 3;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                _line.material = new Material(shader);
            }
            _line.startColor = new Color(0.22f, 1f, 0.61f, 0.9f);
            _line.endColor = new Color(0.22f, 1f, 0.61f, 0.55f);
            RefreshLine();
        }

        // 장착 루어(없으면 lure_normal_01)로 외형을 바꾸고, 낚싯줄 끝(LineEnd)을 그 루어의 고리로 옮긴다.
        // 루어 이미지는 128px·PPU 100·피벗 중앙이라 예전 루어와 같은 크기로 그려진다.
        public void ApplySkin()
        {
            var id = GearVisuals.EquippedOrDefaultId(GearSlot.Lure);
            var sprite = GearVisuals.Load(GearVisuals.WorldPath(GearSlot.Lure, id));
            var renderer = GetComponent<SpriteRenderer>();
            if (sprite == null || renderer == null)
            {
                return;
            }

            renderer.sprite = sprite;
            if (lineEnd != null && GearAnchors.LureEyes.TryGetValue(id, out var eye))
            {
                var half = sprite.rect.width * 0.5f;
                lineEnd.localPosition = new Vector3((eye.x - half) / sprite.pixelsPerUnit, (half - eye.y) / sprite.pixelsPerUnit, 0f);
            }

            RefreshLine();
        }

        public void Tint(Color color)
        {
            if (_line == null)
            {
                return;
            }

            var tip = color;
            tip.a = 0.9f;
            var end = color;
            end.a = 0.5f;
            _line.startColor = tip;
            _line.endColor = end;
        }

        public void Place(Vector3 position)
        {
            transform.position = position;
            RefreshLine();
        }

        public void SetTilt(float degrees)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            RefreshLine();
        }

        void RefreshLine()
        {
            if (_line == null)
            {
                return;
            }

            _line.SetPosition(0, rodTip != null ? rodTip.position : SurfaceLayout.RodTip);
            _line.SetPosition(1, lineEnd != null ? lineEnd.position : transform.position);
        }
    }
}
