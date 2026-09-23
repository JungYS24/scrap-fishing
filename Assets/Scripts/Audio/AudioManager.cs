using UnityEngine;

namespace ScrapFishing.Audio
{
    public class AudioManager : MonoBehaviour
    {
        const string BgmResource = "Audio/Neo-Tokyo Sludge";
        const float BgmVolume = 0.55f;

        public static AudioManager Instance { get; private set; }

        AudioSource _bgm;

        public static AudioManager Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = true;
            _bgm.spatialBlend = 0f;
            _bgm.volume = BgmVolume;
            _bgm.ignoreListenerPause = true;
            _bgm.clip = Resources.Load<AudioClip>(BgmResource);
        }

        public void PlayBgm()
        {
            if (_bgm == null || _bgm.clip == null || _bgm.isPlaying)
            {
                return;
            }

            _bgm.Play();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
