using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ScrapFishing.Gacha
{
    // 오리파 배너 그림(StreamingAssets/Gacha/Banners/*.jpg)을 받아 스프라이트로 만든다.
    // 받기는 동시에 해도 되지만, JPG 풀기(메인 스레드, 장당 수십 ms)는 한 프레임에 한 장만 해서 스크롤 중 프레임이 끊기지 않게 한다.
    // 실행 중 압축(DXT)은 하지 않는다: 도트 그림의 픽셀 경계가 번지고, 웹에서는 압축이 장당 수십 ms 걸린다.
    // 그래서 무압축 RGB24, 밉맵 없음, Point 필터로 두고(장당 약 2.3MB) 메모리는 상점이 개수 상한·닫을 때 해제로 관리한다.
    public static class BannerArtLoader
    {
        struct Pending
        {
            public UnityWebRequest Request;
            public string Name;
            public Action<Sprite> Done;
        }

        static readonly Queue<Pending> Decodes = new Queue<Pending>();
        static Runner _runner;

        // 지금 살아 있는 배너 텍스처 수, 지금까지 보낸 요청 수, 마지막 풀기 시간(테스트·측정용).
        public static int LiveTextures { get; private set; }
        public static int Requests { get; private set; }
        public static double LastDecodeMs { get; private set; }

        public static void Load(string url, string name, Action<Sprite> done)
        {
            Requests++;
            var request = UnityWebRequest.Get(url);
            request.SendWebRequest().completed += _ =>
            {
                Decodes.Enqueue(new Pending { Request = request, Name = name, Done = done });
                EnsureRunner();
            };
        }

        public static void Release(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null)
            {
                Destroy(texture);
                LiveTextures = Mathf.Max(0, LiveTextures - 1);
            }
        }

        static void Destroy(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        static void EnsureRunner()
        {
            if (_runner != null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                // 에디터(플레이 아님)에서는 프레임이 돌지 않으므로 바로 푼다.
                while (Decodes.Count > 0)
                {
                    Decode(Decodes.Dequeue());
                }

                return;
            }

            var go = new GameObject("BannerArtLoader");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        static void Decode(Pending pending)
        {
            var request = pending.Request;
            Sprite sprite = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                var watch = Stopwatch.StartNew();
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                {
                    name = pending.Name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };

                // markNonReadable: GPU에 올린 뒤 CPU 사본을 버린다.
                if (texture.LoadImage(request.downloadHandler.data, true))
                {
                    LiveTextures++;
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    sprite.name = pending.Name;
                }
                else
                {
                    Debug.LogWarning("배너 그림을 풀지 못했습니다: " + request.url);
                    Destroy(texture);
                }

                watch.Stop();
                LastDecodeMs = watch.Elapsed.TotalMilliseconds;
            }
            else
            {
                Debug.LogWarning("배너 그림을 받지 못했습니다: " + request.url + " / " + request.error);
            }

            request.Dispose();
            pending.Done?.Invoke(sprite);
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (Decodes.Count > 0)
                {
                    Decode(Decodes.Dequeue());
                }
            }

            void OnDestroy()
            {
                if (_runner == this)
                {
                    _runner = null;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Decodes.Clear();
            _runner = null;
            LiveTextures = 0;
            Requests = 0;
            LastDecodeMs = 0;
        }
    }
}
