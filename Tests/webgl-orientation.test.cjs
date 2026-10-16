const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const template = fs.readFileSync(path.join(__dirname,
  '../Assets/WebGLTemplates/YandexGames/index.html'), 'utf8');
const source = template.match(/<script>([\s\S]*?)<\/script>/)[1]
  .replace(/^\s*#(?:if|else|endif)\b.*$/gm, '');

function events(target = {}) {
  const listeners = new Map();
  target.addEventListener = (name, callback) => {
    if (!listeners.has(name)) listeners.set(name, []);
    listeners.get(name).push(callback);
  };
  target.removeEventListener = (name, callback) => {
    listeners.set(name, (listeners.get(name) || []).filter(item => item !== callback));
  };
  target.emit = name => {
    for (const callback of listeners.get(name) || []) callback({ preventDefault() {} });
  };
  return target;
}

function fixture({ mobile = true, type = 'portrait-primary', width = 900, height = 420,
  screenWidth = 420, screenHeight = 900, legacy, coarse = mobile, touch = mobile ? 5 : 0,
  language = 'ru', languageChoice = null } = {}) {
  const messages = [], frames = [], timers = [], appended = [];
  let focusCount = 0;
  const canvas = { style: {}, focus() { focusCount++; } };
  const cover = { hidden: true };
  const elements = {
    '#unity-container': { style: {} }, '#unity-canvas': canvas, '#orientation-cover': cover,
    '#loading-cover': { style: {} }, '#unity-progress-bar-empty': { style: {} },
    '#unity-progress-bar-full': { style: {} }, '.spinner': { style: {} },
    '#orientation-title': { textContent: '' }, '#orientation-description': { textContent: '' }
  };
  const document = events({ hidden: false, activeElement: null, documentElement: {},
    querySelector: selector => elements[selector],
    createElement: () => ({ style: {} }), body: { appendChild: element => appended.push(element) } });
  const orientation = events({ type });
  const window = events({ screen: { orientation, width: screenWidth, height: screenHeight },
    innerWidth: width, innerHeight: height, orientation: legacy,
    matchMedia: () => ({ matches: coarse }), visualViewport: events({}), location: { hostname: 'test.invalid' } });
  window.top = window;
  const ctx = vm.createContext({ window, document,
    navigator: { userAgent: mobile ? 'Android Chrome' : 'Desktop Chrome', maxTouchPoints: touch, language },
    localStorage: { getItem: () => languageChoice },
    Element: function Element() {}, requestAnimationFrame: callback => frames.push(callback),
    setTimeout: (callback, delay) => timers.push({ callback, delay }),
    console: { log() {}, error() {} } });
  vm.runInContext(source, ctx, { filename: 'YandexGames/index.html' });
  const run = code => vm.runInContext(code, ctx);
  const flushFrames = () => { while (frames.length) frames.shift()(); };
  return { ctx, run, window, document, orientation, cover, messages, timers, appended, elements,
    focusCount: () => focusCount, flushFrames,
    ready() {
      ctx.testMessages = messages;
      run('ysdk = {}; ygGameInstance = { SendMessage: (...args) => testMessages.push(args) }; InitGame();');
    },
    rotate(next) { orientation.type = next; orientation.emit('change'); },
    lastPause() { return messages.filter(item => item[1] === 'SetPauseGame').at(-1)?.[2]; }
  };
}

test('portrait phone shows a readable guard before Unity loads, even inside a landscape iframe', () => {
  const f = fixture();
  assert.equal(f.cover.hidden, false);
  assert.equal(f.appended.length, 1, 'Unity loader is appended after the guard has already been evaluated');
  assert.equal(f.messages.length, 0, 'No Unity messages before Unity exists');
  assert.match(template, /Поверните телефон/);
  f.run('FocusGame(false)'); f.flushFrames();
  assert.equal(f.focusCount(), 0, 'Orientation cover cannot lose focus to the underlying game');
  f.ready();
  assert.equal(f.lastPause(), 'true', 'A portrait startup pauses as soon as Unity announces readiness');
});

test('orientation text uses a saved language before Unity, then SDK language in automatic mode', () => {
  const english = fixture({ language: 'ru-RU', languageChoice: 'en' });
  assert.equal(english.elements['#orientation-title'].textContent, 'Rotate your phone');
  assert.equal(english.document.documentElement.lang, 'en');
  english.run("ysdk = { environment: { i18n: { lang: 'ru' } } }; SetGameLanguage('en');");
  assert.equal(english.elements['#orientation-title'].textContent, 'Rotate your phone');
  english.run("SetGameLanguage('auto');");
  assert.equal(english.elements['#orientation-title'].textContent, 'Поверните телефон');
  english.run("ysdk.environment.i18n.lang = 'tr'; SetGameLanguage('auto');");
  assert.equal(english.elements['#orientation-description'].textContent, 'Play in landscape mode.');
  english.run("SetGameLanguage('ru', 'en');");
  assert.equal(english.elements['#orientation-title'].textContent, 'Поверните телефон');
  const automatic = fixture({ language: 'en-US' });
  assert.equal(automatic.document.documentElement.lang, 'en');
});

test('real landscape clears the guard even when the iframe viewport is portrait', () => {
  const f = fixture({ type: 'landscape-primary', width: 420, height: 900 });
  assert.equal(f.cover.hidden, true);
  f.ready();
  assert.equal(f.lastPause(), undefined, 'Normal startup does not force-unpause the game');
  f.rotate('portrait-primary');
  assert.equal(f.cover.hidden, false); assert.equal(f.lastPause(), 'true');
  f.rotate('landscape-secondary'); f.flushFrames();
  assert.equal(f.cover.hidden, true); assert.equal(f.lastPause(), 'false');
  assert(f.focusCount() > 0);
});

test('desktop narrow windows are unaffected; coarse touch tablets are recognized without a mobile UA', () => {
  assert.equal(fixture({ mobile: false, width: 420, height: 900 }).cover.hidden, true);
  assert.equal(fixture({ mobile: false, coarse: true, touch: 5 }).cover.hidden, false);
  assert.equal(fixture({ mobile: false, coarse: false, touch: 5 }).cover.hidden, true,
    'A laptop with a touch screen and precise pointer is not forced into mobile orientation');
});

test('legacy orientation, screen dimensions and viewport provide fallbacks', () => {
  assert.equal(fixture({ type: null, legacy: 90 }).cover.hidden, true);
  assert.equal(fixture({ type: null, legacy: 180 }).cover.hidden, false);
  assert.equal(fixture({ type: null }).cover.hidden, false);
  assert.equal(fixture({ type: null, screenWidth: 900, screenHeight: 420 }).cover.hidden, true);
  assert.equal(fixture({ type: null, screenWidth: 0, screenHeight: 0, width: 420, height: 900 }).cover.hidden, false);
  assert.equal(fixture({ type: null, screenWidth: 0, screenHeight: 0 }).cover.hidden, true);
});

test('SDK resume cannot remove portrait pause, and landscape cannot remove an SDK pause', () => {
  const f = fixture(); f.ready();
  f.run('PauseCallback(); ResumeCallback();'); f.flushFrames();
  assert.equal(f.lastPause(), 'true'); assert.equal(f.focusCount(), 0);
  f.run('PauseCallback()');
  f.rotate('landscape-primary'); f.flushFrames();
  assert.equal(f.cover.hidden, true); assert.equal(f.lastPause(), 'true');
  assert.equal(f.focusCount(), 0, 'Rotation does not steal focus from an SDK advertisement');
  f.run('ResumeCallback()'); f.flushFrames();
  assert.equal(f.lastPause(), 'false'); assert(f.focusCount() > 0);
});

test('blur and a hidden document remain paused through rotation and SDK resume', () => {
  const f = fixture(); f.ready();
  f.window.emit('blur');
  f.rotate('landscape-primary'); f.run('ResumeCallback()'); f.flushFrames();
  assert.equal(f.lastPause(), 'true'); assert.equal(f.focusCount(), 0);
  assert(f.messages.some(item => item[1] === 'SetFocusWindowGame' && item[2] === 'false'));
  f.window.emit('focus'); f.flushFrames();
  assert.equal(f.lastPause(), 'false');
  f.document.hidden = true; f.document.emit('visibilitychange');
  const before = f.focusCount();
  f.run('ResumeCallback(); FocusGame(false)'); f.flushFrames();
  assert.equal(f.lastPause(), 'true'); assert.equal(f.focusCount(), before);
  f.document.hidden = false; f.document.emit('visibilitychange');
  assert.equal(f.lastPause(), 'false');
});

test('Unity startup and the delayed startup check re-read orientation without relying on a resize', () => {
  const f = fixture({ type: 'landscape-primary' });
  f.orientation.type = 'portrait-primary';
  f.ready(); assert.equal(f.cover.hidden, false); assert.equal(f.lastPause(), 'true');
  f.orientation.type = 'landscape-primary';
  for (const timer of f.timers.filter(item => item.delay === 100)) timer.callback();
  assert.equal(f.cover.hidden, true); assert.equal(f.lastPause(), 'false');
});
