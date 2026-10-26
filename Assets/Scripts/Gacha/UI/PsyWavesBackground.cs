using ScrapFishing.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ScrapFishing.Gacha
{
    // 뽑기 결과 카드 뒤 사이키델릭 파장 배경. ScrapFishing/UI/PsyWaves 셰이더를 화면의 1/N 해상도 RenderTexture에 그리고
    // RawImage가 Point 필터로 확대해 보여 준다(픽셀아트 느낌 + 셰이더가 계산하는 픽셀 수가 1/N²로 줄어 가볍다).
    // 결과 화면은 timeScale이 0일 수 있어 시간은 unscaled로 쌓아 넘긴다. 숨기면 꺼서(렌더 안 함) RT도 놓는다.
    [RequireComponent(typeof(RawImage))]
    public class PsyWavesBackground : MonoBehaviour
    {
        public const string ShaderResource = "Shaders/PsyWaves";
        // 카드 중심(결과 카드 칸 Grid의 가운데, 화면 기준).
        static readonly Vector4 CardCenter = new Vector4(0.5f, 0.51f, 0f, 0f);

        static readonly int ColorA = Shader.PropertyToID("_ColorA");
        static readonly int ColorB = Shader.PropertyToID("_ColorB");
        static readonly int ColorC = Shader.PropertyToID("_ColorC");
        static readonly int Speed = Shader.PropertyToID("_Speed");
        static readonly int CycleSpeed = Shader.PropertyToID("_CycleSpeed");
        static readonly int Frequency = Shader.PropertyToID("_Frequency");
        static readonly int Swirl = Shader.PropertyToID("_Swirl");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");
        static readonly int CenterDarken = Shader.PropertyToID("_CenterDarken");
        static readonly int PixelSize = Shader.PropertyToID("_PixelSize");
        static readonly int WhiteFade = Shader.PropertyToID("_WhiteFade");
        static readonly int Center = Shader.PropertyToID("_Center");
        static readonly int Aspect = Shader.PropertyToID("_Aspect");
        static readonly int AnimTime = Shader.PropertyToID("_AnimTime");
        static readonly int Opaque = Shader.PropertyToID("_Opaque");

        RawImage _image;
        Material _material;
        RenderTexture _texture;
        GachaVideoConfig _config;
        GachaVideoConfig.PsyPalette _palette;
        ItemRarity _rarity;
        float _shownAt;
        float _phase;

        public ItemRarity Rarity => _rarity;
        public GachaVideoConfig.PsyPalette Palette => _palette;
        public Material Material => _material;
        public RenderTexture Texture => _texture;
        public bool Rendering => isActiveAndEnabled && _texture != null;
        public float Fade { get; private set; }
        public float CurrentSpeed { get; private set; }
        public float CurrentIntensity { get; private set; }

        public static PsyWavesBackground Create(RectTransform parent, GachaVideoConfig config)
        {
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("결과 배경 셰이더를 쓸 수 없어 흰 배경을 그대로 씁니다: Resources/" + ShaderResource);
                return null;
            }

            var rect = GachaUi.Rect(parent, "PsyWaves", Vector2.zero, Vector2.one);
            rect.SetAsFirstSibling();
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.white;
            var background = rect.gameObject.AddComponent<PsyWavesBackground>();
            background._image = image;
            background._config = config;
            background._material = new Material(shader) { hideFlags = HideFlags.DontSave };
            rect.gameObject.SetActive(false);
            return background;
        }

        // 결과 카드가 나타나는 순간 부른다. 흰색(영상의 흰 화면)에서 시작해 파장으로 바뀐다.
        public void Show(ItemRarity rarity)
        {
            _rarity = rarity;
            _palette = _config.PaletteFor(rarity);
            _material.SetColor(ColorA, _palette.a);
            _material.SetColor(ColorB, _palette.b);
            _material.SetColor(ColorC, _palette.c);
            _material.SetFloat(CycleSpeed, _palette.cycle);
            _material.SetFloat(Frequency, _config.psyFrequency);
            _material.SetFloat(Swirl, _config.psySwirl);
            _material.SetFloat(CenterDarken, _config.psyCenterDarken);
            _material.SetFloat(PixelSize, 0f);
            _material.SetVector(Center, CardCenter);
            _material.SetFloat(Opaque, 1f);
            _shownAt = Time.unscaledTime;
            _phase = 0f;
            gameObject.SetActive(true);
            transform.SetAsFirstSibling();
            Tick(0f);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }

            Release();
        }

        void LateUpdate()
        {
            Tick(Time.unscaledDeltaTime);
        }

        void Tick(float dt)
        {
            var elapsed = Time.unscaledTime - _shownAt;
            Fade = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _config.psyFadeIn));
            // 레전드·싱크로: 처음 잠깐 속도·세기를 올렸다가 원래 값으로(펄스).
            var pulse = _rarity >= ItemRarity.Legend ? 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _config.psyPulseDuration)) : 0f;
            pulse = pulse * pulse;
            CurrentSpeed = _palette.speed * Mathf.Lerp(1f, _config.psyPulseSpeed, pulse);
            CurrentIntensity = Mathf.Clamp01(_palette.intensity + _config.psyPulseIntensity * pulse);
            // 속도가 바뀌어도 무늬가 튀지 않게 위상을 쌓아서 넘긴다(셰이더 시간 = _AnimTime × _Speed, _Speed는 1).
            _phase += dt * CurrentSpeed;
            _material.SetFloat(AnimTime, 0.001f + _phase);
            _material.SetFloat(Speed, 1f);
            _material.SetFloat(Intensity, CurrentIntensity);
            _material.SetFloat(WhiteFade, Fade);
            if (EnsureTexture())
            {
                Graphics.Blit(null, _texture, _material);
            }
        }

        // 화면 크기(캔버스 배율 포함)의 1/N 해상도. 칸 크기가 바뀌면 다시 만든다.
        bool EnsureTexture()
        {
            var rect = ((RectTransform)transform).rect;
            var canvas = _image.canvas;
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            var divisor = Mathf.Max(1, _config.psyDownscale);
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale / divisor), 16, 512);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale / divisor), 16, 1024);
            if (rect.width <= 1f || rect.height <= 1f)
            {
                return false;
            }

            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                Release();
                _texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "PsyWavesRT",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
                _texture.Create();
                _image.texture = _texture;
            }

            _material.SetFloat(Aspect, rect.width / rect.height);
            return true;
        }

        void Release()
        {
            if (_texture == null)
            {
                return;
            }

            if (_image != null && _image.texture == _texture)
            {
                _image.texture = null;
            }

            if (RenderTexture.active == _texture)
            {
                RenderTexture.active = null;
            }

            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        void OnDestroy()
        {
            Release();
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
