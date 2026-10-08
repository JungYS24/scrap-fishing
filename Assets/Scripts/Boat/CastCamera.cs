using ScrapFishing.Core;
using UnityEngine;

namespace ScrapFishing.Boat
{
    public class CastCamera : MonoBehaviour
    {
        Camera _camera;
        HookMover _hook;
        GameFlow _flow;
        CastingController _casting;
        Vector3 _rest;

        public void Bind(Camera camera, HookMover hook, GameFlow flow, CastingController casting)
        {
            _camera = camera;
            _hook = hook;
            _flow = flow;
            _casting = casting;
            if (_camera != null)
            {
                _rest = _camera.transform.position;
            }
        }

        public void SnapRest()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.transform.position = _rest;
            _camera.backgroundColor = Palette.Background;
        }

        void LateUpdate()
        {
            if (_camera == null || _flow == null)
            {
                return;
            }

            var launching = _casting != null && _casting.IsLaunching;
            var follow = _hook != null && !launching && (_flow.Phase == GamePhase.Casting || _flow.Phase == GamePhase.Reeling);
            if (!follow)
            {
                MoveTo(_rest.y, 10f);
                _camera.backgroundColor = Palette.Background;
                return;
            }

            var targetY = _hook.Position.y + (_rest.y - SurfaceLayout.WaterlineY);
            if (targetY > _rest.y)
            {
                targetY = _rest.y;
            }

            MoveTo(targetY, 14f);
            var depth = Mathf.InverseLerp(SurfaceLayout.WaterlineY, SurfaceLayout.MaxDepthY, _hook.Position.y);
            _camera.backgroundColor = Color.Lerp(Palette.Water, Palette.DeepWater, depth);
        }

        void MoveTo(float y, float rate)
        {
            var pos = _camera.transform.position;
            pos.y = Mathf.Lerp(pos.y, y, 1f - Mathf.Exp(-rate * Time.deltaTime));
            _camera.transform.position = pos;
        }
    }
}
