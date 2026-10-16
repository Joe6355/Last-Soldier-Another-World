mergeInto(LibraryManager.library, {
    GameLocalePreference_js: function () {
        var choice = '';
        try { choice = localStorage.getItem('LastSoldier.Language') || ''; } catch (_) { }
        var buffer = _malloc(lengthBytesUTF8(choice) + 1);
        stringToUTF8(choice, buffer, lengthBytesUTF8(choice) + 1);
        return buffer;
    },
    GameLocaleSelection_js: function (choicePtr, resolvedPtr) {
        var choice = UTF8ToString(choicePtr);
        var language = UTF8ToString(resolvedPtr);
        try { localStorage.setItem('LastSoldier.Language', choice); } catch (_) { }
        if (typeof window.SetGameLanguage === 'function') window.SetGameLanguage(choice, language);
    }
});
