// 처음 실행할 때 고를 언어를 위해 브라우저 언어(navigator.language, 예: "ko-KR", "en-US")를 돌려준다. 못 읽으면 빈 문자열.
mergeInto(LibraryManager.library, {
  ScrapFishing_BrowserLanguage: function () {
    var lang = "";
    try {
      lang = navigator.language || (navigator.languages && navigator.languages[0]) || "";
    } catch (err) {}
    var size = lengthBytesUTF8(lang) + 1;
    var buffer = _malloc(size);
    stringToUTF8(lang, buffer, size);
    return buffer;
  }
});
