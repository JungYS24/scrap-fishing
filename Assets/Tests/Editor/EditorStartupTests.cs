using NUnit.Framework;
using UnityEditor;

namespace ScrapFishing.Tests
{
    public class EditorStartupTests
    {
        [Test]
        public void GameView_HasPortraitSizeForCurrentTarget()
        {
            PortraitGameViewSize.EnsureSize();
            var group = PortraitGameViewSize.CurrentGroup();
            Assert.IsNotNull(group, "지금 빌드 대상의 Game 뷰 크기 목록");
            Assert.GreaterOrEqual(PortraitGameViewSize.IndexOf(group, 480, 854), 0, "480x854가 드롭다운에 있다");
        }

        [Test]
        public void BootScene_IsBoatAndFirstInBuild()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(PortraitGameViewSize.BootScene));
            Assert.AreEqual(PortraitGameViewSize.BootScene, EditorBuildSettings.scenes[0].path);
        }
    }
}
