using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.UI
{
    // UI Image용 프레임 재생기. 월드의 SpriteAnimator와 같은 프레임 배열을 쓴다.
    // 결과 화면처럼 timeScale이 0인 화면에서도 돌도록 unscaled 시간을 쓰고, 시작 프레임은 무작위로 해서 여러 칸이 똑같이 움직이지 않게 한다.
    public class UiSpriteAnimator : MonoBehaviour
    {
        Image _image;
        Sprite[] _frames;
        float _fps;
        float _time;

        public static UiSpriteAnimator Play(Image image, Sprite[] frames, float fps)
        {
            if (image == null || frames == null || frames.Length == 0)
            {
                return null;
            }

            var player = image.GetComponent<UiSpriteAnimator>();
            if (player == null)
            {
                player = image.gameObject.AddComponent<UiSpriteAnimator>();
            }

            player._image = image;
            player._frames = frames;
            player._fps = Mathf.Max(0.1f, fps);
            player._time = Random.value * frames.Length / player._fps;
            player.Apply();
            return player;
        }

        public int FrameIndex => _frames != null && _frames.Length > 0 ? Mathf.FloorToInt(_time * _fps) % _frames.Length : 0;

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _image == null)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;
            Apply();
        }

        void Apply()
        {
            var frame = _frames[FrameIndex];
            if (frame != null && _image.sprite != frame)
            {
                _image.sprite = frame;
            }
        }
    }
}
