(function (root) {
  'use strict';
  function resolve(manifest, action, phase, milliseconds) {
    const macro = action === 'chase' || action === 'armor';
    const name = action === 'chase' ? 'grab' : action === 'armor' ? 'idle' : action;
    const clip = manifest.clips[name];
    if (!clip || !['normal', 'crimson'].includes(phase)) throw new Error('Unknown action or phase');
    const repeats = action === 'chase' ? 3 : 1;
    const duration = clip.totalMs * repeats + (action === 'chase' ? 900 : 0);
    const t = Math.max(0, Math.min(milliseconds, duration - .001));
    const cycle = Math.min(repeats - 1, Math.floor(t / clip.totalMs));
    const local = Math.min(clip.totalMs - .001, t - cycle * clip.totalMs);
    let frame = 0, end = clip.durationsMs[0];
    while (frame < 5 && local >= end) end += clip.durationsMs[++frame];
    let sheet = clip.kind === 'laser' ? 'laser' : clip.kind === 'main' ? phase : 'special-' + phase;
    if (name === 'enrage') sheet = 'special-normal';
    if (name === 'defeat') sheet = 'special-crimson';
    let row = clip.kind === 'laser' ? (phase === 'crimson' ? 1 : 0) : clip.row;
    const override=clip.frameOverrides?.[phase]?.[frame];
    let sourceFrame=frame;
    if(override){sheet=override.sheet;row=override.row;sourceFrame=override.column;}
    return {action, name, clip, sheet, row, sourceFrame, frame, cycle, local, t, duration,
      loop: macro || clip.loop, progress: t / duration,
      scale: name === 'defeat' && frame >= 4 ? (frame === 4 ? .6 : .45) : 1};
  }
  function laserAt(milliseconds) {
    const age = milliseconds - 1500;
    const firing = age >= 0 && age < 180;
    return {charge: Math.max(0,Math.min(1,milliseconds/1500)),
      aiming: milliseconds >= 0 && milliseconds < 1500,
      locked: milliseconds >= 1100 && milliseconds < 1500,
      firing, age,
      beamAlpha: firing ? (age < 45 ? 1 : Math.pow(Math.max(0,1-(age-45)/135),.6)) : 0,
      impact: age >= 0 && age < 450 ? age/450 : -1,
      kick: age >= 0 && age < 130 ? 1-age/130 : 0};
  }
  function status(state) {
    if (state.action === 'armor') return 'Светлые сочленения и основание хвоста — уязвимые зоны';
    if (state.name === 'infernal-shot') return state.t < 1100 ? 'Наведение · подготовка 1,5 с'
      : state.t < 1500 ? 'Направление зафиксировано · уклоняйся' : state.t < 1680 ? 'Белый лазер · импакт' : 'Восстановление';
    if (state.name === 'piercing-sting') return state.t < 1000 ? 'Подготовка укола · 1 с'
      : state.frame < 4 ? 'Укол' : state.frame === 4 ? 'Жало в земле · окно уязвимости' : 'Возврат хвоста';
    if (state.name === 'tail-break') return state.frame >= 4 ? 'Хвост разрушен · хвостовые атаки недоступны' : 'Разрушение хвоста';
    if (state.name === 'tail-regrow') return state.frame === 5 ? 'Хвост восстановлен · ударная волна' : 'Восстановление сегментов';
    if (state.name === 'defeat') return state.frame >= 4 ? 'Маленькая Шаула · больше не атакует' : 'Панцирь рассыпается · атаки прекращены';
    if (state.name === 'enrage') return state.frame >= 4 ? 'Багровая форма · до конца боя' : 'Переход во вторую фазу при 50% HP';
    if (state.action === 'chase') return state.t >= state.clip.totalMs*3 ? 'Комбинация закончена · окно уязвимости' : 'Рывок ' + (state.cycle+1) + ' из 3';
    if (state.name === 'grab') return state.frame < 2 ? 'Раскрывает клешни · предупреждение' : state.frame < 4 ? 'Захват' : 'Восстановление после выпада';
    if (state.name === 'slam') return state.frame < 2 ? 'Поднимает клешни · отойди' : state.frame < 4 ? 'Удар по земле' : 'Восстановление';
    if (state.name === 'star-barrage') return state.frame < 2 ? 'Метки мест падения' : 'Звёздный обстрел';
    if (state.name === 'needle-volley') return state.frame < 1 ? 'Наведение хвоста' : 'Очередь магических игл';
    return state.clip.label;
  }
  const api = {resolve,status,laserAt};
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.ShaulaPreviewLogic = api;
})(typeof window === 'object' ? window : {});
