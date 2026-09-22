using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ScrapFishing.Core
{
    public class FixedResolution : MonoBehaviour
    {
        public static FixedResolution Current { get; private set; }

        Camera _gameCamera;
        Camera _letterboxCamera;

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

            var letterboxGo = new GameObject("LetterboxCamera");
            letterboxGo.transform.SetParent(transform, false);
            _letterboxCamera = letterboxGo.AddComponent<Camera>();
            _letterboxCamera.orthographic = true;
            _letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            _letterboxCamera.backgroundColor = Color.black;
            _letterboxCamera.cullingMask = 0;
            _letterboxCamera.depth = _gameCamera.depth - 1;
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

            var window = Screen.width / (float)Mathf.Max(1, Screen.height);
            if (window > GameView.Aspect)
            {
                var width = GameView.Aspect / window;
                _gameCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                var height = window / GameView.Aspect;
                _gameCamera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }
    }
}
