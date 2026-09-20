using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ScrapFishing.Core
{
    public class FixedResolution : MonoBehaviour
    {
        public static FixedResolution Current { get; private set; }

        Camera _gameCamera;
        RenderTexture _rt;

        public Camera GameCamera => _gameCamera;

        public static bool ContainsWindowPoint(Vector2 windowPosition)
        {
            if (Current == null)
            {
                return true;
            }

            return Current.LetterboxRect().Contains(windowPosition);
        }

        public static Vector2 ToGamePixels(Vector2 windowPosition)
        {
            if (Current == null)
            {
                return windowPosition;
            }

            var rect = Current.LetterboxRect();
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
            var game = ToGamePixels(windowPosition);
            var cam = camera != null ? camera : Camera.main;
            return cam.ScreenToWorldPoint(new Vector3(game.x, game.y, 10f));
        }

        public void Attach(Camera gameCamera)
        {
            Current = this;
            _gameCamera = gameCamera;
            _rt = new RenderTexture(GameView.Width, GameView.Height, 16)
            {
                name = "GameViewRT",
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };

            _gameCamera.orthographic = true;
            _gameCamera.orthographicSize = GameView.OrthoSize;
            _gameCamera.targetTexture = _rt;

            var clearGo = new GameObject("LetterboxCamera");
            clearGo.transform.SetParent(transform, false);
            var clearCamera = clearGo.AddComponent<Camera>();
            clearCamera.orthographic = true;
            clearCamera.clearFlags = CameraClearFlags.SolidColor;
            clearCamera.backgroundColor = Color.black;
            clearCamera.cullingMask = 0;
            clearCamera.depth = _gameCamera.depth + 1;
            clearCamera.allowHDR = false;
            clearCamera.allowMSAA = false;
            var urp = clearGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderType = CameraRenderType.Base;
            urp.renderShadows = false;
            urp.renderPostProcessing = false;

            var canvasGo = new GameObject("LetterboxCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            canvasGo.AddComponent<CanvasScaler>();

            var background = CreateFullRect(canvasGo.transform, "Bars");
            var bars = background.gameObject.AddComponent<Image>();
            bars.color = Color.black;
            bars.raycastTarget = false;

            var view = CreateFullRect(background, "GameView");
            var raw = view.gameObject.AddComponent<RawImage>();
            raw.texture = _rt;
            raw.raycastTarget = false;
            var fitter = view.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = GameView.Aspect;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (_gameCamera != null)
            {
                _gameCamera.targetTexture = null;
            }

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
        }

        Rect LetterboxRect()
        {
            var scale = Mathf.Min(Screen.width / (float)GameView.Width, Screen.height / (float)GameView.Height);
            var width = GameView.Width * scale;
            var height = GameView.Height * scale;
            return new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
        }

        static RectTransform CreateFullRect(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
