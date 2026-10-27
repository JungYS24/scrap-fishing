using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ScrapFishing.Boat;
using ScrapFishing.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    // 보트 씬을 플레이 모드로 띄워 "60초가 지나면 결과창이 보이는지"를 확인한다.
    public class ResultsFlowTests
    {
        const string BoatScene = "Assets/Scenes/Boat.unity";

        [UnityTest]
        public IEnumerator Results_ShowAfterTimeUp_TwiceInARow()
        {
            yield return new EnterPlayMode();
            for (var round = 0; round < 2; round++)
            {
                var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BoatScene, new LoadSceneParameters(LoadSceneMode.Single));
                while (!load.isDone)
                {
                    yield return null;
                }

                yield return null;
                yield return null;
                var boot = Object.FindFirstObjectByType<BoatBootstrap>();
                Assert.IsNotNull(boot, "BoatBootstrap");
                typeof(BoatBootstrap).GetMethod("BeginRun", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
                yield return null;

                var session = boot.GetComponent<RunSession>();
                var flow = boot.GetComponent<GameFlow>();
                Assert.AreEqual(GamePhase.Aiming, flow.Phase, "round " + round);
                session.Tick(RunSession.Duration + 1f);
                yield return new WaitForSecondsRealtime(1.5f);

                Assert.AreEqual(GamePhase.Results, flow.Phase, "round " + round);
                var results = GameObject.Find("Results");
                var report = Describe(results);
                Debug.Log("[ResultsFlowTests] round " + round + "\n" + report);
                Assert.IsNotNull(results, "Results 오브젝트");
                Assert.IsTrue(results.activeInHierarchy, report);
                Assert.Greater(EffectiveAlpha(results.transform), 0.95f, report);
                Assert.Greater(results.transform.lossyScale.x, 0.01f, report);
                Assert.IsNull(Cover(results.transform), report);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Results_ShowWhenTimeEndsWhileCasting()
        {
            yield return new EnterPlayMode();
            yield return LoadAndBegin();
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            var flow = boot.GetComponent<GameFlow>();
            boot.GetComponent<CastingController>().CastFromGauge();
            yield return new WaitForSecondsRealtime(0.3f);
            Debug.Log("[ResultsFlowTests] casting phase=" + flow.Phase);
            boot.GetComponent<RunSession>().Tick(RunSession.Duration + 1f);
            yield return WaitForResults(flow, 20f);
            AssertVisible("casting");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Results_ShowWhenTimeEndsWhileDiving()
        {
            yield return new EnterPlayMode();
            yield return LoadAndBegin();
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            var flow = boot.GetComponent<GameFlow>();
            var session = boot.GetComponent<RunSession>();
            typeof(RunSession).GetProperty("DiveProgress").SetValue(session, 3);
            typeof(BoatBootstrap).GetMethod("HandleDiveSwipe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(GamePhase.Diving, flow.Phase);
            session.Tick(RunSession.Duration + 1f);
            yield return WaitForResults(flow, 20f);
            AssertVisible("diving");
            yield return new ExitPlayMode();
        }

        // 실제 플레이처럼 잡은 것이 있으면(카운트업·전리품 칸·싱크로·최고 기록) 결과창이 뜨는지. 저장된 달러·최고 기록은 되돌린다.
        [UnityTest]
        public IEnumerator Results_ShowWithLootAndNewBest()
        {
            var dollars = PlayerPrefs.GetInt(Progression.DollarKey, 0);
            var best = PlayerPrefs.GetInt("BestCY", 0);
            yield return new EnterPlayMode();
            yield return LoadAndBegin();
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            var flow = boot.GetComponent<GameFlow>();
            var session = boot.GetComponent<RunSession>();
            PlayerPrefs.SetInt("BestCY", 0);
            foreach (var item in ScrapFishing.Data.ItemDatabase.Instance.Items)
            {
                if (item != null && item.Chip > 0)
                {
                    session.AddSettledCatch(null, item, Vector3.zero, out _, out _);
                }
            }

            session.Tick(RunSession.Duration + 1f);
            yield return WaitForResults(flow, 5f);
            yield return new WaitForSecondsRealtime(4f);
            AssertVisible("loot");
            yield return new ExitPlayMode();
            PlayerPrefs.SetInt(Progression.DollarKey, dollars);
            PlayerPrefs.SetInt("BestCY", best);
            PlayerPrefs.Save();
        }

        // 강화 3종을 모두 최대로 올린 저장으로 한 판을 끝내도 결과창이 뜨는지(풀강 후 60초가 끝나면 멈추던 버그).
        // richDollars: 쿠폰을 여러 번 써서 달러가 int 최대값 근처인 경우.
        [UnityTest]
        public IEnumerator Results_ShowWhenAllUpgradesMaxed([Values(false, true)] bool richDollars)
        {
            var dollars = PlayerPrefs.GetInt(Progression.DollarKey, 0);
            var best = PlayerPrefs.GetInt("BestCY", 0);
            // 플레이 모드에 들어가며 도메인이 다시 읽혀도 남도록 배열 대신 값 하나씩 둔다.
            var reel = PlayerPrefs.GetInt(Progression.LevelKey(UpgradeKind.Reel), 0);
            var boat = PlayerPrefs.GetInt(Progression.LevelKey(UpgradeKind.Boat), 0);
            var depth = PlayerPrefs.GetInt(Progression.LevelKey(UpgradeKind.Depth), 0);
            PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Reel), Progression.MaxUpgradeLevel);
            PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Boat), Progression.MaxUpgradeLevel);
            PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Depth), Progression.MaxUpgradeLevel);
            PlayerPrefs.SetInt(Progression.DollarKey, richDollars ? int.MaxValue - 10 : 0);
            try
            {
                yield return new EnterPlayMode();
                yield return LoadAndBegin();
                var boot = Object.FindFirstObjectByType<BoatBootstrap>();
                var flow = boot.GetComponent<GameFlow>();
                var session = boot.GetComponent<RunSession>();
                foreach (var item in ScrapFishing.Data.ItemDatabase.Instance.Items)
                {
                    if (item != null && item.Chip > 0)
                    {
                        session.AddSettledCatch(null, item, Vector3.zero, out _, out _);
                    }
                }

                session.Tick(RunSession.Duration + 1f);
                yield return WaitForResults(flow, 5f);
                yield return new WaitForSecondsRealtime(4f);
                AssertVisible("maxed rich=" + richDollars);
                Assert.GreaterOrEqual(Progression.Dollars, 0, "달러가 넘쳐 음수가 되면 안 된다");
                yield return new ExitPlayMode();
            }
            finally
            {
                PlayerPrefs.SetInt(Progression.DollarKey, dollars);
                PlayerPrefs.SetInt("BestCY", best);
                PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Reel), reel);
                PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Boat), boat);
                PlayerPrefs.SetInt(Progression.LevelKey(UpgradeKind.Depth), depth);

                PlayerPrefs.Save();
            }
        }

        static IEnumerator LoadAndBegin()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BoatScene, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();
            typeof(BoatBootstrap).GetMethod("BeginRun", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return null;
        }

        static IEnumerator WaitForResults(GameFlow flow, float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (flow.Phase != GamePhase.Results && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.AreEqual(GamePhase.Results, flow.Phase, "시간 종료 뒤 Results로 가야 한다");
            yield return new WaitForSecondsRealtime(1.5f);
        }

        static void AssertVisible(string label)
        {
            var results = GameObject.Find("Results");
            var report = Describe(results);
            Debug.Log("[ResultsFlowTests] " + label + "\n" + report);
            Assert.IsNotNull(results, label);
            Assert.IsTrue(results.activeInHierarchy, report);
            Assert.Greater(EffectiveAlpha(results.transform), 0.95f, report);
            Assert.Greater(results.transform.lossyScale.x, 0.01f, report);
            Assert.IsNull(Cover(results.transform), report);
        }

        static float EffectiveAlpha(Transform t)
        {
            var alpha = 1f;
            for (var p = t; p != null; p = p.parent)
            {
                var group = p.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    alpha *= group.alpha;
                    if (group.ignoreParentGroups)
                    {
                        break;
                    }
                }
            }

            return alpha;
        }

        // 결과창보다 뒤에 그려지는(형제 순서가 뒤인) 보이는 그래픽이 화면을 덮고 있는지.
        static string Cover(Transform results)
        {
            var parent = results.parent;
            if (parent == null)
            {
                return null;
            }

            for (var i = results.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                var sibling = parent.GetChild(i);
                if (!sibling.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foreach (var graphic in sibling.GetComponentsInChildren<Graphic>())
                {
                    var rect = graphic.rectTransform.rect;
                    if (graphic.enabled && graphic.color.a * EffectiveAlpha(graphic.transform) > 0.5f
                        && rect.width >= 400f && rect.height >= 700f)
                    {
                        return sibling.name + "/" + graphic.name + " alpha=" + graphic.color.a;
                    }
                }
            }

            return null;
        }

        static string Describe(GameObject results)
        {
            var text = new StringBuilder();
            if (results == null)
            {
                return "Results 없음";
            }

            text.Append("active=").Append(results.activeInHierarchy)
                .Append(" alpha=").Append(EffectiveAlpha(results.transform))
                .Append(" scale=").Append(results.transform.lossyScale)
                .Append(" sibling=").Append(results.transform.GetSiblingIndex())
                .Append(" timeScale=").Append(Time.timeScale)
                .Append(" cover=").Append(Cover(results.transform) ?? "없음").Append('\n');
            var parent = results.transform.parent;
            if (parent != null)
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    text.Append("  [").Append(i).Append("] ").Append(child.name)
                        .Append(child.gameObject.activeInHierarchy ? " on" : " off")
                        .Append(" alpha=").Append(EffectiveAlpha(child)).Append('\n');
                }
            }

            return text.ToString();
        }
    }
}
