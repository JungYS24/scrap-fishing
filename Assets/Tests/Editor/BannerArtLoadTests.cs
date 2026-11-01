using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ScrapFishing.Boat;
using ScrapFishing.Gacha;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ScrapFishing.Tests
{
    public class BannerArtLoadTests
    {
        // EnterPlayMode가 도메인을 다시 불러오면 람다가 잡은 지역 변수가 사라지므로, 답은 static 필드·메서드로 받는다.
        static int _answers;
        static bool _gotSprite;

        static void Answer(Sprite sprite)
        {
            _answers++;
            _gotSprite |= sprite != null;
        }

        // 없는 그림(404와 같은 실패): 동시에 두 번 불러도 한 번만 받고, 실패는 MaxBannerAttempts번까지만 다시 받는다.
        [UnityTest]
        public IEnumerator MissingArt_RequestsOnceAtATime_AndStopsRetrying()
        {
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            var banner = new GachaBanner { Id = "banner_missing_test", BannerImage = "Gacha/Banners/banner_missing_test" };
            var start = BannerArtLoader.Requests;
            for (var attempt = 1; attempt <= GachaBanner.MaxBannerAttempts; attempt++)
            {
                _answers = 0;
                _gotSprite = false;
                banner.LoadBannerSprite(Answer);
                banner.LoadBannerSprite(Answer);
                var until = Time.realtimeSinceStartup + 5f;
                while (_answers < 2 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }

                Assert.AreEqual(2, _answers, "두 요청 모두 답을 받는다");
                Assert.IsFalse(_gotSprite, "실패하면 null(카드는 기본 그림을 쓴다)");
                Assert.AreEqual(attempt, BannerArtLoader.Requests - start, "동시에 부른 두 번은 한 번만 받는다");
            }

            Assert.IsTrue(banner.BannerGaveUp);
            _answers = 0;
            banner.LoadBannerSprite(Answer);
            Assert.AreEqual(1, _answers, "포기한 뒤에는 바로 null");
            Assert.AreEqual(GachaBanner.MaxBannerAttempts, BannerArtLoader.Requests - start, "더는 받지 않는다(무한 재시도 없음)");
            LogAssert.ignoreFailingMessages = false;
            yield return new ExitPlayMode();
        }

        // 오리파를 열고 닫기를 10번 반복해도 배너 텍스처·메모리가 늘지 않는다(닫을 때 모두 내림).
        [UnityTest]
        public IEnumerator OpenClose10Times_DoesNotGrowTextures()
        {
            yield return new EnterPlayMode();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Boat.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            var open = typeof(BoatBootstrap).GetMethod("OpenMarket", BindingFlags.Instance | BindingFlags.NonPublic);
            int firstTextures = 0;
            long firstMemory = 0;
            var maxLive = 0;
            for (var i = 0; i < 10; i++)
            {
                open.Invoke(boot, null);
                var view = Object.FindFirstObjectByType<GachaShopView>();
                var until = Time.realtimeSinceStartup + 3f;
                while (BannerArtLoader.LiveTextures < 3 && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                }

                maxLive = Mathf.Max(maxLive, BannerArtLoader.LiveTextures);
                view.Close();
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(0, BannerArtLoader.LiveTextures, "닫으면 배너 그림을 모두 내린다");
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
                var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                var memory = Profiler.GetTotalAllocatedMemoryLong();
                if (i == 0)
                {
                    firstTextures = textures;
                    firstMemory = memory;
                }

                Debug.Log($"[BannerMemory] {i + 1}회 닫은 뒤 텍스처 {textures}개, 할당 {memory / 1024f / 1024f:0.0}MB, 열었을 때 배너 {maxLive}장");
            }

            System.GC.Collect();
            var lastTextures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            var lastMemory = Profiler.GetTotalAllocatedMemoryLong();
            Assert.Greater(maxLive, 0, "열면 보이는 배너를 받는다");
            Assert.LessOrEqual(lastTextures, firstTextures, "열고 닫기를 반복해도 텍스처 수가 늘지 않는다");
            Assert.Less(lastMemory - firstMemory, 8L * 1024 * 1024, "메모리가 계속 늘지 않는다(배너 3장 ≈ 7MB보다 작게)");
            yield return new ExitPlayMode();
        }
    }
}
