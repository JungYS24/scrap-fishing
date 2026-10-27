// 페이지(index.html)가 documentElement로 켠 전체화면을 게임 안에서 확인하고 끈다.
mergeInto(LibraryManager.library, {
  ScrapFishing_IsFullscreen: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  },

  ScrapFishing_ExitFullscreen: function () {
    if (!(document.fullscreenElement || document.webkitFullscreenElement)) {
      return;
    }
    var exit = document.exitFullscreen || document.webkitExitFullscreen;
    if (!exit) {
      return;
    }
    try {
      var pending = exit.call(document);
      if (pending && pending.catch) {
        pending.catch(function () {});
      }
    } catch (err) {}
  }
});
