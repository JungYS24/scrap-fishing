using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ScrapFishing.Tests
{
    // 갈무리 폰트는 쓰는 글자만 남긴 서브셋이다(Tools/FontSubset). 번역 시트·데이터에 폰트에 없는 글자가 생기면 알려 준다.
    // 실패하면: cd Tools/FontSubset && npm install && node subset.mjs
    public class FontCoverageTests
    {
        static readonly string[] Fonts = { "Fonts/Galmuri11", "Fonts/Galmuri14" };

        [Test]
        public void SubsetFontsCoverGameText()
        {
            var text = new HashSet<char>();
            foreach (var file in Directory.GetFiles("Assets/Resources", "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".csv") || f.EndsWith(".json"))
                         .Concat(Directory.GetFiles("Assets/Art", "*.json", SearchOption.AllDirectories)))
            {
                foreach (var c in File.ReadAllText(file))
                {
                    if (!char.IsControl(c) && !char.IsSurrogate(c) && c != '﻿')
                    {
                        text.Add(c);
                    }
                }
            }

            foreach (var path in Fonts)
            {
                var font = Resources.Load<Font>(path);
                Assert.IsNotNull(font, path);
                var missing = text.Where(c => !font.HasCharacter(c)).OrderBy(c => c).ToArray();
                Assert.IsEmpty(missing, path + "에 없는 글자: " + new string(missing));
            }
        }
    }
}
