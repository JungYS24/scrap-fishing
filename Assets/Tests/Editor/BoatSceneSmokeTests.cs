using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ScrapFishing.Audio;
using ScrapFishing.Boat;
using ScrapFishing.Core;
using ScrapFishing.Dive;
using ScrapFishing.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScrapFishing.Tests
{
    // 보트 씬을 플레이 모드로 띄워 타이틀 버튼 배치, 설정창 음소거·슬라이더, 보트·잠수 드론을 확인한다.
    public class BoatSceneSmokeTests
    {
        const string BoatScene = "Assets/Scenes/Boat.unity";

        [UnityTest]
        public IEnumerator TitleSettingsAndDrones()
        {
            var soundOn = PlayerPrefs.GetInt("SoundOn", 1);
            yield return new EnterPlayMode();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(BoatScene, new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            yield return null;
            var boot = Object.FindFirstObjectByType<BoatBootstrap>();

            // 달러 패널: ReadyPanel 밖으로 나와 있고, 오리파를 열어도 그 위에 보인다. 오리파 상단 로고·달러 패널은 숨긴다.
            var dollar = GameObject.Find("Dollor_Panel").transform;
            Assert.AreNotEqual("ReadyPanel", dollar.parent.name, "출항 중에도 보이도록 ReadyPanel 밖");
            typeof(BoatBootstrap).GetMethod("OpenMarket", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return null;
            yield return null;
            var market = GameObject.Find("BlackMarket").transform;
            Assert.IsTrue(dollar.gameObject.activeInHierarchy);
            Assert.AreSame(market.parent, dollar.parent);
            Assert.Greater(dollar.GetSiblingIndex(), market.GetSiblingIndex(), "오리파 위에 달러 패널");
            var logo = market.Find("TopBar/Logo");
            var marketDollar = market.Find("TopBar/DollarPanel");
            Assert.IsTrue(logo == null || !logo.gameObject.activeSelf, "BLACK MARKET 글자 숨김");
            Assert.IsTrue(marketDollar == null || !marketDollar.gameObject.activeSelf, "오리파 달러 패널 숨김");
            market.Find("TopBar/Close").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.4f);

            // 타이틀: 오른쪽 위에는 설정 버튼만, 음소거 버튼은 없다.
            var title = GameObject.Find("Title").transform;
            var gear = (RectTransform)title.Find("SettingsButton");
            Assert.IsNotNull(gear, "Title/SettingsButton");
            Assert.AreEqual(new Vector2(0.86f, 0.91f), gear.anchorMin);
            Assert.AreEqual(new Vector2(0.98f, 0.98f), gear.anchorMax);
            Assert.IsNull(title.Find("SoundToggle"), "타이틀에서 음소거 버튼은 설정창으로 옮겼다");

            // 결과 화면: 재도전·오리파 버튼이 타이틀의 출항·오리파 버튼과 같은 패널 이미지·틴트를 쓴다.
            var results = Object.FindFirstObjectByType<ResultsView>(FindObjectsInactive.Include);
            var resultsRoot = GameObject.Find("Canvas").GetComponentsInChildren<Transform>(true);
            Transform restart = null;
            Transform resultsMarket = null;
            foreach (var t in resultsRoot)
            {
                if (t.name == "RestartButton" && t.parent.name == "Results")
                {
                    restart = t;
                }
                else if (t.name == "MarketButton" && t.parent.name == "Results")
                {
                    resultsMarket = t;
                }
            }

            Assert.IsNotNull(results);
            Assert.IsNotNull(restart, "Results/RestartButton");
            Assert.IsNotNull(resultsMarket, "Results/MarketButton");
            var titleStart = title.GetComponent<TitleView>().StartButton.GetComponent<Image>();
            var titleOripa = title.GetComponent<TitleView>().MarketButton.GetComponent<Image>();
            Assert.IsNotNull(titleStart.sprite, "타이틀 출항 패널");
            Assert.AreEqual(titleStart.sprite, restart.GetComponent<Image>().sprite, "재도전 = 출항 패널");
            Assert.AreEqual(titleStart.color, restart.GetComponent<Image>().color);
            Assert.AreEqual(titleOripa.sprite, resultsMarket.GetComponent<Image>().sprite, "결과 오리파 = 타이틀 오리파 패널");
            Assert.AreEqual(titleOripa.color, resultsMarket.GetComponent<Image>().color);
            var restartRect = (RectTransform)restart;
            var marketRect = (RectTransform)resultsMarket;
            Assert.AreEqual(restartRect.anchorMax.y - restartRect.anchorMin.y, marketRect.anchorMax.y - marketRect.anchorMin.y, 1e-4f, "두 버튼 같은 높이");
            Assert.AreEqual(TitleView.MarketGap, restartRect.anchorMin.y - marketRect.anchorMax.y, 1e-4f, "타이틀과 같은 간격");

            // 설정창: 음량 머리글 옆 음소거, 음소거 상태가 머리글·버튼 아이콘에 반영된다.
            typeof(BoatBootstrap).GetMethod("OpenSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.4f);
            var panel = GameObject.Find("Settings").transform.Find("Panel");

            // 쿠폰: 크레딧 아래 버튼 → 입력 창이 열린다(실제 달러는 건드리지 않는다).
            var credits = (RectTransform)panel.Find("Credits");
            var coupon = (RectTransform)panel.Find("Coupon");
            Assert.IsNotNull(coupon, "Settings/Panel/Coupon");
            Assert.Less(coupon.anchorMax.y, credits.anchorMin.y, "크레딧 아래");
            coupon.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            var couponOverlay = GameObject.Find("Settings").transform.Find("CouponOverlay");
            Assert.IsTrue(couponOverlay.gameObject.activeSelf);
            Assert.IsNotNull(couponOverlay.Find("Panel/Input").GetComponent<InputField>());
            couponOverlay.Find("Panel/Cancel").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(couponOverlay.gameObject.activeSelf);
            var toggle = panel.Find("SoundToggle");
            Assert.IsNotNull(toggle, "Settings/Panel/SoundToggle");
            var header = panel.Find("VolumeHeader").GetComponent<Text>();
            // 스킨 음소거 버튼은 글자 없이 스피커 그림(켜짐/꺼짐)만 바뀐다.
            var sound = toggle.GetComponent<SettingsSoundButton>();
            Assert.IsNotNull(sound, "스킨 음소거 버튼");
            Assert.IsNull(toggle.GetComponentInChildren<Text>(), "♪ 글자 삭제");
            AudioManager.Ensure().SetSoundOn(true);
            Assert.AreEqual("음량", header.text);
            Assert.IsTrue(sound.ShowsOn);
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(AudioManager.Ensure().SoundOn);
            Assert.AreEqual("음량 · 음소거 중", header.text);
            Assert.IsFalse(sound.ShowsOn);
            Assert.AreEqual(0.4f, panel.Find("Row_Master").GetComponent<CanvasGroup>().alpha, 1e-4f);
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("음량", header.text);
            Assert.IsTrue(sound.ShowsOn);

            // 슬라이더 손잡이: 세 줄 모두 트랙 높이의 1.6배로 보이고, 터치는 슬라이더 칸 전체.
            Canvas.ForceUpdateCanvases();
            foreach (var row in new[] { "Row_Master", "Row_Bgm", "Row_Sfx" })
            {
                var rowSlider = panel.Find(row + "/Slider").GetComponent<Slider>();
                var trackHeight = ((RectTransform)rowSlider.transform.Find("Track")).rect.height;
                Assert.AreEqual(trackHeight * SettingsView.KnobTrackRatio, rowSlider.handleRect.rect.height, 0.5f, row);
                Assert.AreEqual(Vector3.one, rowSlider.handleRect.localScale, row + " 스케일로 줄이지 않는다");
            }

            var slider = panel.Find("Row_Master/Slider").GetComponent<Slider>();
            var hitHeight = ((RectTransform)slider.transform).rect.height;
            Assert.Greater(hitHeight, slider.handleRect.rect.height, "터치 영역이 보이는 손잡이보다 크다");
            Assert.GreaterOrEqual(hitHeight, 43f, "터치 영역은 예전(약 43px) 이상");
            var panelRect = ((RectTransform)panel).rect;
            Assert.LessOrEqual(((RectTransform)slider.transform).rect.width, panelRect.width);
            panel.Find("Close").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(panel.gameObject.activeInHierarchy, "타이틀 설정창 닫힘");
            Assert.AreEqual(0f, Time.timeScale, "타이틀에서 닫아도 타이틀의 정지 상태는 그대로");

            // 보트: 피셔 머리 왼쪽 위 드론.
            var nomad = GameObject.Find("Nomad").transform;
            var boatDrone = nomad.Find("CompanionDrone");
            Assert.IsNotNull(boatDrone, "Nomad/CompanionDrone");
            var head = nomad.Find("FisherHead");
            var boatRenderer = boatDrone.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(boatRenderer.sprite);
            Assert.Less(boatDrone.position.x, head.position.x, "몸 왼쪽");
            Assert.Greater(boatDrone.position.y, head.position.y - 0.05f, "머리보다 위");
            Assert.AreEqual(9, boatRenderer.sortingOrder);

            // 보트 씬 순서: 보트(뒤) → 피셔 → 낚싯대(앞), 루어는 장착 루어 외형.
            var boatOrder = nomad.Find("BoatLayer").GetComponent<SpriteRenderer>().sortingOrder;
            var fisherOrder = nomad.Find("FisherLayer").GetComponent<SpriteRenderer>().sortingOrder;
            var rodOrder = nomad.Find("RodLayer").GetComponent<SpriteRenderer>().sortingOrder;
            Assert.Less(boatOrder, fisherOrder);
            Assert.Less(fisherOrder, rodOrder);
            Assert.Less(fisherOrder, boatRenderer.sortingOrder, "드론은 피셔보다 앞");
            var lure = GameObject.Find("Lure").GetComponent<SpriteRenderer>();
            Assert.AreEqual(ScrapFishing.Gacha.GearVisuals.EquippedOrDefaultId(ScrapFishing.Gacha.GearSlot.Lure), lure.sprite.name);

            // 잠수: 같은 드론이 잠수부를 따라오고, 잠수부가 왼쪽을 보면 뒤집힌다.
            typeof(BoatBootstrap).GetMethod("BeginRun", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.3f);
            var session = boot.GetComponent<RunSession>();

            // 출항 후 보트: HUD 오른쪽 위는 음소거 대신 톱니 버튼, 누르면 설정창이 열리고 게임이 멈춘다.
            var hud = GameObject.Find("Hud").transform;
            Assert.IsNull(hud.Find("SoundToggle"), "HUD 음소거 버튼은 톱니 버튼으로 바꿨다");
            yield return OpenSettingsFromHud(hud, "보트");

            // HUD 패널(보트): 예전 글자 HUD 대신 TIME·수심·구역·스크랩·잠수 칸 패널. 잠수 칸은 CastsPerDive만큼.
            foreach (var old in new[] { "Time", "Depth", "ScrapLoad", "DiveProgress" })
            {
                Assert.IsNull(hud.Find(old), "예전 글자 HUD 삭제: " + old);
            }

            foreach (var hudPanel in new[] { "TimePanel", "DepthPanel", "ZoneTag", "ScrapPanel", "DiveCells" })
            {
                Assert.IsTrue(hud.Find(hudPanel).gameObject.activeInHierarchy, "보트: " + hudPanel);
            }

            var boatPanels = Object.FindFirstObjectByType<HudView>().Panels;
            Assert.AreEqual(session.CastsPerDive, boatPanels.DiveCells.Count);
            StringAssert.IsMatch("^[0-9]+$", boatPanels.TimeText.text, "TIME은 숫자만");
            StringAssert.IsMatch("^[0-9]+$", boatPanels.DepthText.text, "수심은 숫자만");
            StringAssert.Contains("/", boatPanels.ScrapText.text);
            var boatScrapSize = ((RectTransform)hud.Find("ScrapPanel")).rect.size;
            // 힌트는 아래 가운데, 수심 패널·구역 태그보다 아래.
            var hint = (RectTransform)hud.Find("Hint");
            Assert.AreEqual(TextAnchor.MiddleCenter, hint.GetComponent<Text>().alignment);
            Assert.AreEqual(0.5f, (hint.anchorMin.x + hint.anchorMax.x) * 0.5f, 1e-4f, "가운데");
            var hintCorners = new Vector3[4];
            var tagCorners = new Vector3[4];
            hint.GetWorldCorners(hintCorners);
            ((RectTransform)hud.Find("ZoneTag")).GetWorldCorners(tagCorners);
            Assert.Less(hintCorners[1].y, tagCorners[0].y, "힌트가 구역 태그 아래");
            var boatTimeSize = ((RectTransform)hud.Find("TimePanel")).rect.size;

            typeof(RunSession).GetProperty("DiveProgress").SetValue(session, 3);
            typeof(BoatBootstrap).GetMethod("HandleDiveSwipe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(dollar.gameObject.activeInHierarchy, "잠수 중에도 달러 패널이 보인다");

            // HUD 패널(잠수): TIME·스크랩·수심은 같은 모양·크기, 수심은 해저 평원 4000m 이상, 잠수 칸만 숨김.
            Assert.IsTrue(hud.Find("TimePanel").gameObject.activeInHierarchy, "잠수: TIME");
            Assert.IsTrue(hud.Find("ScrapPanel").gameObject.activeInHierarchy, "잠수: 스크랩");
            Assert.AreEqual(boatScrapSize, ((RectTransform)hud.Find("ScrapPanel")).rect.size);
            Assert.AreEqual(boatTimeSize, ((RectTransform)hud.Find("TimePanel")).rect.size);
            Assert.IsTrue(hud.Find("DepthPanel").gameObject.activeInHierarchy, "잠수: 수심 패널 유지");
            Assert.IsTrue(hud.Find("ZoneTag").gameObject.activeInHierarchy, "잠수: 구역 태그 유지");
            Assert.AreEqual("해저 평원", boatPanels.ZoneText.text);
            Assert.GreaterOrEqual(int.Parse(boatPanels.DepthText.text), 4000, "잠수 수심은 4000m 이상");
            Assert.LessOrEqual(int.Parse(boatPanels.DepthText.text), 4100);
            Assert.IsFalse(hud.Find("DiveCells").gameObject.activeInHierarchy, "잠수: 잠수 칸 숨김");
            StringAssert.Contains("/", boatPanels.ScrapText.text, "잠수 중에도 스크랩 숫자");
            Assert.IsFalse(GameObject.Find("Canvas").transform.Find("GameArea/ReadyPanel").gameObject.activeSelf, "강화 버튼은 출항 중 숨김");
            Assert.IsFalse(boatDrone.gameObject.activeInHierarchy, "잠수 중에는 보트 드론이 보트와 같이 숨는다");
            var dive = Object.FindFirstObjectByType<DiveDirector>();
            var diveDrone = dive.transform.Find("CompanionDrone");
            Assert.IsNotNull(diveDrone, "DiveDirector/CompanionDrone");
            var diver = GameObject.Find("Player").GetComponent<SpriteRenderer>();
            var droneRenderer = diveDrone.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(droneRenderer.sprite);
            Assert.Less(Vector3.Distance(diveDrone.position, diver.transform.position), 1.5f, "잠수부 근처를 따라온다");
            diver.flipX = true;
            yield return null;
            Assert.IsTrue(droneRenderer.flipX, "잠수부가 왼쪽을 보면 드론도 뒤집힌다");
            Assert.Greater(droneRenderer.sortingOrder, diver.sortingOrder);

            // 헬멧: 잠수부 자식, 장착 헬멧 외형, 잠수부 바로 위 order, 좌우 반전 따라감.
            var helmet = diver.transform.Find("Helmet").GetComponent<SpriteRenderer>();
            Assert.AreEqual(ScrapFishing.Gacha.GearVisuals.EquippedOrDefaultId(ScrapFishing.Gacha.GearSlot.Helmet), helmet.sprite.name);
            Assert.AreEqual(diver.sortingOrder + 1, helmet.sortingOrder);
            Assert.IsTrue(helmet.flipX);
            Assert.Less(helmet.sortingOrder, droneRenderer.sortingOrder);

            // 잠수 중에도 HUD 톱니로 설정창을 열면 멈추고, 닫으면 다시 움직인다.
            yield return OpenSettingsFromHud(hud, "잠수");

            yield return new ExitPlayMode();
            PlayerPrefs.SetInt("SoundOn", soundOn);
            PlayerPrefs.Save();
        }

        static IEnumerator OpenSettingsFromHud(Transform hud, string where)
        {
            var gear = hud.Find("SettingsButton");
            Assert.IsNotNull(gear, where + " Hud/SettingsButton");
            Assert.IsTrue(gear.gameObject.activeInHierarchy, where + " 톱니 버튼이 보인다");
            Assert.AreEqual(1f, Time.timeScale, where + " 플레이 중");
            gear.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            var settings = GameObject.Find("Settings");
            Assert.IsNotNull(settings, where + " 설정창이 열린다");
            // 맨 앞 유지 요소(달러 패널, UiStayOnTop)만 설정창 위에 있을 수 있다.
            var parent = settings.transform.parent;
            for (var i = settings.transform.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                Assert.IsNotNull(parent.GetChild(i).GetComponent<UiStayOnTop>(), where + " 설정창 위에는 달러 패널만: " + parent.GetChild(i).name);
            }
            Assert.IsNotNull(settings.transform.Find("Panel/SoundToggle").GetComponent<SettingsSoundButton>(), where + " 새 스킨 패널");
            Assert.AreEqual(0f, Time.timeScale, where + " 설정창이 열려 있는 동안 멈춤");
            settings.transform.Find("Panel/Close").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(settings.activeSelf, where + " 닫힘");
            Assert.AreEqual(1f, Time.timeScale, where + " 닫으면 다시 움직인다");
        }
    }
}
