using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ScrapFishing.Core
{
    public class FixedResolution : MonoBehaviour
    {
        public static FixedResolution Current { get; private set; }

        Camera _gameCamera;
        Camera _letterboxCamera;
        Rect _appliedRect;
        bool _hasRect;

        public Camera GameCamera => _gameCamera;

        public static bool ContainsWindowPoint(Vector2 windowPosition)
        {
            if (Current == null || Current._gameCamera == null)
            {
                return true;
            }

            return Current._gameCamera.pixelRect.Contains(windowPosition);
        }

        public static Vector2 ToGamePixels(Vector2 windowPosition)
        {
            if (Current == null || Current._gameCamera == null)
            {
                return windowPosition;
            }

            var rect = Current._gameCamera.pixelRect;
            if (rect.width < 1f || rect.height < 1f)
            {
                return windowPosition;
            }

            return new Vector2(
                (windowPosition.x - rect.x) / rect.width * GameView.Width,
                (windowPosition.y - rect.y) / rect.height * GameView.Height);
        }

        public static Vector3 WindowToWorld(Camera camera, Vector2 windowPosition)
        {
            var cam = camera != null ? camera : Camera.main;
            return cam.ScreenToWorldPoint(new Vector3(windowPosition.x, windowPosition.y, 10f));
        }

        public void Attach(Camera gameCamera)
        {
            Current = this;
            _gameCamera = gameCamera;
            _gameCamera.orthographic = true;
            _gameCamera.orthographicSize = GameView.OrthoSize;
            _gameCamera.targetTexture = null;
            _gameCamera.allowHDR = false;
            _gameCamera.allowMSAA = false;
            _gameCamera.clearFlags = CameraClearFlags.SolidColor;
            _gameCamera.depth = 0f;

            var letterboxGo = new GameObject("LetterboxCamera");
            letterboxGo.transform.SetParent(transform, false);
            _letterboxCamera = letterboxGo.AddComponent<Camera>();
            _letterboxCamera.orthographic = true;
            _letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            _letterboxCamera.backgroundColor = Palette.Background;
            _letterboxCamera.cullingMask = 0;
            _letterboxCamera.depth = -10f;
            _letterboxCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _letterboxCamera.allowHDR = false;
            _letterboxCamera.allowMSAA = false;
            var urp = letterboxGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            urp.renderShadows = false;
            urp.renderPostProcessing = false;

            ApplyViewport();
        }

        void LateUpdate()
        {
            ApplyViewport();
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        void ApplyViewport()
        {
            if (_gameCamera == null)
            {
                return;
            }

            var sw = Mathf.Max(1f, Screen.width);
            var sh = Mathf.Max(1f, Screen.height);
            var safe = Screen.safeArea;
            if (safe.width < 2f || safe.height < 2f)
            {
                safe = new Rect(0f, 0f, sw, sh);
            }

            var safeAspect = safe.width / Mathf.Max(1f, safe.height);
            float x;
            float y;
            float w;
            float h;
            if (safeAspect > GameView.Aspect)
            {
                h = safe.height;
                w = h * GameView.Aspect;
                x = safe.x + (safe.width - w) * 0.5f;
                y = safe.y;
            }
            else
            {
                w = safe.width;
                h = w / GameView.Aspect;
                x = safe.x;
                y = safe.y + (safe.height - h) * 0.5f;
            }

            var rect = new Rect(x / sw, y / sh, w / sw, h / sh);
            if (_hasRect && Near(_appliedRect, rect))
            {
                return;
            }

            _hasRect = true;
            _appliedRect = rect;
            _gameCamera.rect = rect;
        }

        static bool Near(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.0005f
                && Mathf.Abs(a.y - b.y) < 0.0005f
                && Mathf.Abs(a.width - b.width) < 0.0005f
                && Mathf.Abs(a.height - b.height) < 0.0005f;
        }
    }
}
