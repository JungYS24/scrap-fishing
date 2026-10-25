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
            var icon = toggle.GetComponentInChildren<Text>();
            AudioManager.Ensure().SetSoundOn(true);
            Assert.AreEqual("음량", header.text);
            Assert.AreEqual(SoundToggle.OnGlyph, icon.text);
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(AudioManager.Ensure().SoundOn);
            Assert.AreEqual("음량 · 음소거 중", header.text);
            Assert.AreEqual(SoundToggle.OffGlyph, icon.text);
            Assert.AreEqual(0.4f, panel.Find("Row_Master").GetComponent<CanvasGroup>().alpha, 1e-4f);
            toggle.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("음량", header.text);

            // 슬라이더 핸들: 세 줄 모두 스케일 0.6, 터치는 슬라이더 줄 전체.
            foreach (var row in new[] { "Row_Master", "Row_Bgm", "Row_Sfx" })
            {
                var handle = panel.Find(row + "/Slider").GetComponent<Slider>().handleRect;
                Assert.AreEqual(new Vector3(0.6f, 0.6f, 1f), handle.localScale, row);
            }

            var slider = panel.Find("Row_Master/Slider").GetComponent<Slider>();
            var hitHeight = ((RectTransform)slider.transform).rect.height;
            Assert.Greater(hitHeight, slider.handleRect.sizeDelta.y * 0.6f, "터치 영역이 보이는 핸들보다 크다");
            var panelRect = ((RectTransform)panel).rect;
            Assert.LessOrEqual(((RectTransform)slider.transform).rect.width, panelRect.width);

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
            yield return null;
            var session = boot.GetComponent<RunSession>();
            typeof(RunSession).GetProperty("DiveProgress").SetValue(session, 3);
            typeof(BoatBootstrap).GetMethod("HandleDiveSwipe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(boot, null);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(dollar.gameObject.activeInHierarchy, "잠수 중에도 달러 패널이 보인다");
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

            yield return new ExitPlayMode();
            PlayerPrefs.SetInt("SoundOn", soundOn);
            PlayerPrefs.Save();
        }
    }
}
