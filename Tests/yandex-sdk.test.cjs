const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const root = path.join(__dirname, '../Assets/PluginYourGames/Modules/');

async function cloudChecks() {
  const calls = [];
  const ctx = vm.createContext({ NO_DATA: 'no data', ysdk: {}, setTimeout, clearTimeout, console: { error() {} },
    YG2Instance: (...args) => calls.push(args), player: null });
  vm.runInContext(fs.readFileSync(root + 'Storage/Scripts/Editor/CopyCode/CloudStorage_js.js', 'utf8'), ctx);
  const save = { progressVersion: 1, ownerId: 'user-1', idSave: 7,
    intKeys: ['Coins'], intValues: [250], floatKeys: [], floatValues: [], stringKeys: [], stringValues: [] };
  ctx.player = { getData: async () => ({ saves: [JSON.stringify(save)] }), setData: async () => {} };
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, 1);
  await ctx.SaveCloud(JSON.stringify(save), true);
  assert(calls.some(call => call[0] === 'GameCloudSaved' && JSON.parse(call[1]).idSave === 7));
  ctx.player.setData = async () => { throw new Error('offline'); };
  await ctx.SaveCloud(JSON.stringify(save), true);
  assert.equal(calls.at(-1)[0], 'GameCloudFailed');
  ctx.player.getData = async () => { throw new Error('offline'); };
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, -1);
  let written = false;
  ctx.player.setData = async () => { written = true; };
  await ctx.SaveCloud(JSON.stringify(save), true);
  assert.equal(written, false, 'Failed cloud load must prevent overwriting remote progress');
  ctx.player.getData = async () => ({ saves: ['broken-json'] });
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, -1);
  ctx.player.getData = async () => ({ saves: ['null'] });
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, -1);
  ctx.player.getData = async () => ({});
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, 1, 'An empty account differs from a network error');
  assert.equal(ctx.cloudSaves, 'no data');
  let finishLoad;
  ctx.player.getData = () => new Promise(resolve => { finishLoad = resolve; });
  const delayed = ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, -2, 'Pending authorization load is distinguishable from an unavailable SDK');
  finishLoad({});
  await delayed;
  assert.equal(ctx.gameCloudStatus, 1);
}

async function authChecks() {
  const calls = [];
  const guest = { isAuthorized: () => false, getUniqueID: () => 'guest-1' };
  const ctx = vm.createContext({ NO_DATA: 'no data', console: { error() {} },
    YG2Instance: (...args) => calls.push(args), LogStyledMessage() {},
    ysdk: { getPlayer: async () => guest, auth: { openAuthDialog: async () => { throw new Error('cancelled'); } } } });
  vm.runInContext(fs.readFileSync(root + 'Authorization/Scripts/Editor/CopyCode/Auth_js.js', 'utf8'), ctx);
  await ctx.OpenAuthDialog();
  assert.equal(calls.at(-1)[0], 'GameAuthClosed');
  assert(!calls.some(call => call[0] === 'LoggedIn'));
  assert.equal(JSON.parse(calls.find(call => call[0] === 'SetAuth')[1]).playerAuth, 'rejected');
}

async function leaderboardChecks() {
  const calls = [];
  const submissions = [];
  const ctx = vm.createContext({ NO_DATA: 'no data', console: { error() {} }, LogStyledMessage() {},
    YG2Instance: (...args) => calls.push(args),
    ysdk: { isAvailableMethod: async () => true, leaderboards: {
      setScore: async (...args) => submissions.push(args),
      getDescription: async () => ({ default: true, description: { sort_order: 'DESC',
        score_format: { type: 'numeric', options: { decimal_offset: 0 } } } }),
      getEntries: async () => ({ entries: [{ rank: 1, score: 323, extraData: '{"waves":3,"kills":23}',
        player: { uniqueID: 'user-1', publicName: 'Тестовый игрок', getAvatarSrc: () => '' } }] })
    } } });
  vm.runInContext(fs.readFileSync(root + 'Leaderboards/Scripts/Editor/CopyCode/LB_js.js', 'utf8'), ctx);
  await ctx.SetLeaderboard('mmr', 323, '{"waves":3,"kills":23}');
  assert.deepEqual(submissions[0], ['mmr', 323, '{"waves":3,"kills":23}']);
  assert.equal(calls.at(-1)[0], 'GameRatingSaved');
  ctx.ysdk.isAvailableMethod = async () => false;
  await ctx.SetLeaderboard('mmr', 324, '');
  assert.equal(submissions.length, 1, 'Unavailable submission does not overwrite score');
  assert.equal(calls.at(-1)[0], 'GameRatingFailed');
  ctx.GetLeaderboard('mmr', 15, 1, 'small', false);
  await new Promise(setImmediate);
  const rows = JSON.parse(calls.at(-1)[1]);
  assert.equal(rows.technoName, 'mmr');
  assert.equal(rows.type, 'numeric');
  assert.equal(rows.isInvertSortOrder, false);
  assert.equal(rows.names[0], 'Тестовый игрок');
  assert.equal(rows.extraDataArray[0], '{"waves":3,"kills":23}');
  ctx.ysdk.leaderboards.getDescription = async () => { throw new Error('offline'); };
  ctx.GetLeaderboard('mmr', 15, 1, 'small', false);
  await new Promise(setImmediate);
  assert.deepEqual(calls.at(-1), ['GameLeaderboardFailed', 'mmr']);
  ctx.ysdk = null;
  ctx.GetLeaderboard('mmr', 15, 1, 'small', false);
  assert.deepEqual(calls.at(-1), ['GameLeaderboardFailed', 'mmr']);
}

async function advertisementChecks() {
  const calls = [];
  let rewardCallbacks, interCallbacks, interRequests = 0;
  const ctx = vm.createContext({ initGame: true, console: { warn() {}, error() {} }, FocusGame() {},
    YG2Instance: (...args) => calls.push(args),
    ysdk: { adv: { showRewardedVideo: ({ callbacks }) => { rewardCallbacks = callbacks; },
      showFullscreenAdv: ({ callbacks }) => { interRequests++; interCallbacks = callbacks; } } } });
  for (const module of ['RewardedAdv', 'InterstitialAdv']) {
    const file = module === 'RewardedAdv' ? 'RewardedAdv_js.js' : 'InterAdv_js.js';
    vm.runInContext(fs.readFileSync(root + module + '/Scripts/Editor/CopyCode/' + file, 'utf8'), ctx);
  }
  ctx.RewardedAdvShow('continue_wave_full_heal');
  rewardCallbacks.onOpen();
  rewardCallbacks.onClose();
  assert(!calls.some(call => call[0] === 'RewardAdv'), 'Cancelled video grants no continuation');
  calls.length = 0;
  ctx.RewardedAdvShow('continue_wave_full_heal');
  rewardCallbacks.onError(new Error('unavailable'));
  rewardCallbacks.onClose();
  assert.deepEqual(calls.map(call => call[0]), ['ErrorRewardedAdv', 'CloseRewardedAdv']);
  calls.length = 0;
  ctx.RewardedAdvShow('continue_wave_full_heal');
  rewardCallbacks.onOpen();
  rewardCallbacks.onRewarded();
  rewardCallbacks.onRewarded();
  rewardCallbacks.onClose();
  rewardCallbacks.onRewarded();
  assert.equal(calls.filter(call => call[0] === 'RewardAdv').length, 1, 'Reward is granted exactly once');
  assert.equal(calls.find(call => call[0] === 'RewardAdv')[1], 'continue_wave_full_heal');
  calls.length = 0;
  ctx.InterAdvShow();
  ctx.InterAdvShow();
  assert.equal(interRequests, 1, 'Concurrent fullscreen requests are blocked before onOpen');
  interCallbacks.onOpen();
  interCallbacks.onError(new Error('unavailable'));
  interCallbacks.onClose(false);
  assert.deepEqual(calls.map(call => call[0]), ['OpenInterAdv', 'ErrorInterAdv', 'CloseInterAdv']);
  assert.equal(calls.at(-1)[1], 'false');
  calls.length = 0;
  ctx.ysdk = null;
  ctx.RewardedAdvShow('continue_wave_full_heal');
  assert.deepEqual(calls.map(call => call[0]), ['ErrorRewardedAdv', 'CloseRewardedAdv']);
  const bannerCalls = [];
  let finishShow;
  ctx.ysdk = { adv: { showBannerAdv: () => { bannerCalls.push('show'); return new Promise(resolve => { finishShow = resolve; }); },
    hideBannerAdv: async () => { bannerCalls.push('hide'); } } };
  vm.runInContext(fs.readFileSync(root + 'StickyAdv/Scripts/Editor/CopyCode/StickyAdv_js.js', 'utf8'), ctx);
  const banner = ctx.StickyAdActivity(true);
  await ctx.StickyAdActivity(false);
  finishShow({ stickyAdvIsShowing: true });
  await banner;
  assert.deepEqual(bannerCalls, ['show', 'hide'], 'Latest menu/game banner visibility wins');
}

(async () => {
  await cloudChecks();
  await authChecks();
  await leaderboardChecks();
  await advertisementChecks();
  console.log('PASS: cloud, migration protection, authorization, leaderboard MMR/data/errors, video cancellation/reward/error, fullscreen concurrency and banner ordering');
})().catch(error => { console.error(error); process.exitCode = 1; });
