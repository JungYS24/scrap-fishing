using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ScrapFishing.Core;
using ScrapFishing.Data;
using ScrapFishing.Gacha;
using UnityEngine;

namespace ScrapFishing.Tests
{
    public class LocalizationTests
    {
        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?:[,:][^}]*)?\}");

        [TearDown]
        public void TearDown()
        {
            // 직접 넣은 표를 지우고 Resources의 실제 표로 돌아간다. 언어는 LocTestSetup이 고정한 한국어로.
            Loc.LoadTable(null);
            Loc.SetLanguage(GameLanguage.Ko, false);
        }

        [Test]
        public void CsvParsesQuotesNewlinesAndBom()
        {
            var csv = "﻿key,ko,en\r\n"
                + "a.plain,가,A\r\n"
                + "a.comma,\"가, 나\",\"A, B\"\r\n"
                + "a.newline,\"첫 줄\r\n둘째 줄\",\"line 1\nline 2\"\r\n"
                + "a.quote,\"\"\"인용\"\"\",\"say \"\"hi\"\"\"\r\n"
                + "a.space, · 뒤 공백 , trailing \r\n"
                + "a.empty_en,한국어만,\r\n"
                + "\r\n";
            var rows = Loc.ParseCsv(csv);
            Assert.AreEqual(7, rows.Count, "머리글 + 6줄(빈 줄은 건너뜀)");
            Assert.AreEqual("key", rows[0][0], "BOM은 첫 칸에 붙지 않는다");

            Loc.LoadTable(csv);
            Loc.SetLanguage(GameLanguage.Ko, false);
            Assert.AreEqual("가, 나", Loc.T("a.comma"));
            Assert.AreEqual("첫 줄\n둘째 줄", Loc.T("a.newline"), "따옴표 안 줄바꿈(\\r\\n)은 \\n 하나로");
            Assert.AreEqual("\"인용\"", Loc.T("a.quote"));
            Assert.AreEqual(" · 뒤 공백 ", Loc.T("a.space"), "앞뒤 공백을 지우지 않는다");

            Loc.SetLanguage(GameLanguage.En, false);
            Assert.AreEqual("A, B", Loc.T("a.comma"));
            Assert.AreEqual("line 1\nline 2", Loc.T("a.newline"));
            Assert.AreEqual("say \"hi\"", Loc.T("a.quote"));
            Assert.AreEqual("한국어만", Loc.T("a.empty_en"), "영어 칸이 비면 한국어");
        }

        [Test]
        public void MissingKeyReturnsKeyAndOrFallsBack()
        {
            Assert.AreEqual("ui.no_such_key", Loc.T("ui.no_such_key"));
            Assert.AreEqual("기존 이름", Loc.Or("gear.no_such.name", "기존 이름"));
            Assert.AreEqual("ui.no_such_key", Loc.F("ui.no_such_key", 1, 2));
        }

        [Test]
        public void FormatErrorsDoNotThrow()
        {
            Loc.LoadTable("key,ko,en\nbad,{0} {1,ko,{0} {1\n");
            Assert.DoesNotThrow(() => Loc.F("bad", 1));
        }

        [Test]
        public void TableHasEveryRowFromSheet()
        {
            var keys = Loc.Keys.ToList();
            Assert.GreaterOrEqual(keys.Count, 505, "마스터 시트 505줄");
            foreach (var key in keys)
            {
                Assert.IsFalse(string.IsNullOrEmpty(Loc.Ko(key)), key + " 한국어");
                Assert.IsFalse(string.IsNullOrEmpty(Loc.En(key)), key + " 영어");
            }
        }

        [Test]
        public void PlaceholdersMatchBetweenLanguages()
        {
            foreach (var key in Loc.Keys)
            {
                CollectionAssert.AreEquivalent(Indices(Loc.Ko(key)), Indices(Loc.En(key)), key + " {n} 번호 집합");
                Assert.AreEqual(Count(Loc.Ko(key), "<color"), Count(Loc.En(key), "<color"), key + " <color> 태그 수");
                Assert.AreEqual(Count(Loc.Ko(key), "</color>"), Count(Loc.En(key), "</color>"), key + " </color> 태그 수");
            }
        }

        [Test]
        public void EveryGearMachineAndBannerHasKeys()
        {
            var catalog = GachaCatalog.LoadDefault();
            Assert.Greater(catalog.AllGears.Count, 0);
            foreach (var gear in catalog.AllGears)
            {
                Assert.IsTrue(Loc.Has("gear." + gear.Id + ".name"), gear.Id + " 이름 키");
                Assert.IsTrue(Loc.Has("gear." + gear.Id + ".desc"), gear.Id + " 설명 키");
            }

            foreach (var machine in catalog.Machines)
            {
                var key = GachaVideoConfig.KeyOf(machine.Id);
                Assert.IsTrue(Loc.Has("machine." + key + ".name"), machine.Id + " 이름 키");
                Assert.IsTrue(Loc.Has("machine." + key + ".copy"), machine.Id + " 문구 키");
            }

            Assert.Greater(catalog.Banners.Count, 0);
            foreach (var banner in catalog.Banners)
            {
                Assert.IsTrue(Loc.Has("banner." + banner.Id + ".name"), banner.Id + " 이름 키");
                Assert.IsTrue(Loc.Has("banner." + banner.Id + ".desc"), banner.Id + " 설명 키");
            }
        }

        [Test]
        public void EveryScrapItemHasKey()
        {
            var database = Resources.Load<ItemDatabase>("ItemDatabase");
            Assert.IsNotNull(database);
            foreach (var item in database.Items)
            {
                Assert.IsTrue(Loc.Has("scrap." + item.Id + ".name"), item.Id + " 이름 키");
            }

            for (var i = 1; i <= 12; i++)
            {
                Assert.IsTrue(Loc.Has("scrap.fallback." + i.ToString("00")), "대체 스크랩 " + i);
            }
        }

        [Test]
        public void DataNamesFollowLanguage()
        {
            var catalog = GachaCatalog.LoadDefault();
            var gear = catalog.AllGears[0];
            var banner = catalog.Banners[0];
            Loc.SetLanguage(GameLanguage.Ko, false);
            Assert.AreEqual(Loc.Ko("gear." + gear.Id + ".name"), gear.DisplayName);
            Assert.AreEqual(Loc.Ko("banner." + banner.Id + ".name"), banner.Name);
            Loc.SetLanguage(GameLanguage.En, false);
            Assert.AreEqual(Loc.En("gear." + gear.Id + ".name"), gear.DisplayName);
            Assert.AreEqual(Loc.En("banner." + banner.Id + ".desc"), banner.Desc);
            Assert.AreEqual("Today's machine: x", Loc.F("ui.shop.daily_machine", "x"));
        }

        // 코드에 적은 "ui.…" 키가 모두 표에 있는지(튜토리얼처럼 접두어만 적은 곳은 .title이 있으면 통과).
        [Test]
        public void EveryUiKeyInCodeExists()
        {
            var literal = new Regex("\"(ui\\.[a-z0-9_.]+)\"");
            var missing = new List<string>();
            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in literal.Matches(File.ReadAllText(file)))
                {
                    var key = match.Groups[1].Value.TrimEnd('.');
                    if (!Loc.Has(key) && !Loc.Has(key + ".title"))
                    {
                        missing.Add(Path.GetFileName(file) + ": " + key);
                    }
                }
            }

            CollectionAssert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void ChangedFiresOnceOnSwitch()
        {
            Loc.SetLanguage(GameLanguage.Ko, false);
            var fired = 0;
            void Count() => fired++;
            Loc.Changed += Count;
            try
            {
                Loc.SetLanguage(GameLanguage.En, false);
                Assert.AreEqual(1, fired);
                Assert.AreEqual("Settings", Loc.T("ui.settings.title"));
                Assert.AreEqual("Language: English", Loc.T("ui.settings.language"));
                Loc.SetLanguage(GameLanguage.En, false);
                Assert.AreEqual(1, fired, "같은 언어면 알리지 않는다");
                Loc.SetLanguage(GameLanguage.Ko, false);
                Assert.AreEqual(2, fired);
                Assert.AreEqual("설정", Loc.T("ui.settings.title"));
            }
            finally
            {
                Loc.Changed -= Count;
            }
        }

        [Test]
        public void LanguageIsSavedInPlayerPrefs()
        {
            var saved = PlayerPrefs.GetString(Loc.PrefKey);
            try
            {
                Loc.Language = GameLanguage.En;
                Assert.AreEqual("en", PlayerPrefs.GetString(Loc.PrefKey));
                Loc.Reload();
                Assert.AreEqual(GameLanguage.En, Loc.Language, "다시 읽어도 영어");
                Loc.Toggle();
                Assert.AreEqual("ko", PlayerPrefs.GetString(Loc.PrefKey));
            }
            finally
            {
                PlayerPrefs.SetString(Loc.PrefKey, saved);
                Loc.Reload();
            }
        }

        static List<int> Indices(string text)
        {
            return Placeholder.Matches(text ?? string.Empty).Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(i => i).ToList();
        }

        static int Count(string text, string part)
        {
            return Regex.Matches(text ?? string.Empty, Regex.Escape(part)).Count;
        }
    }
}
