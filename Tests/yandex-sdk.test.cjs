const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const root = path.join(__dirname, '../Assets/PluginYourGames/Modules/');

async function cloudChecks() {
  const calls = [];
  const logs = [];
  const ctx = vm.createContext({ NO_DATA: 'no data', ysdk: {}, setTimeout, clearTimeout, console: { error() {} },
    LogStyledMessage: message => logs.push(message),
    YG2Instance: (...args) => calls.push(args), player: null });
  vm.runInContext(fs.readFileSync(root + 'Storage/Scripts/Editor/CopyCode/CloudStorage_js.js', 'utf8'), ctx);
  const save = { progressVersion: 1, ownerId: 'user-1', idSave: 7,
    intKeys: ['Coins'], intValues: [250], floatKeys: [], floatValues: [], stringKeys: [], stringValues: [] };
  ctx.player = { getData: async () => ({ saves: [JSON.stringify(save)] }), setData: async () => {} };
  await ctx.LoadCloud();
  assert.equal(ctx.gameCloudStatus, 1);
  await ctx.SaveCloud(JSON.stringify(save), true);
  assert(calls.some(call => call[0] === 'GameCloudSaved' && JSON.parse(call[1]).idSave === 7));
  assert(logs.some(message => message.startsWith('Cloud state loaded: revision=7 coins=250')));
  assert(logs.some(message => message.startsWith('Cloud state save confirmed: revision=7 coins=250')));
  assert(!logs.join('\n').includes(save.ownerId), 'Cloud diagnostics omit the account ID');
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
  const queries = [];
  const verificationNames = [];
  const logs = [], warnings = [], errors = [];
  const delayedChecks = [];
  const ctx = vm.createContext({ NO_DATA: 'no data',
    setTimeout: (callback, delay) => delayedChecks.push({ callback, delay }),
    console: { error: (...args) => errors.push(args.join(' ')), warn: (...args) => warnings.push(args.join(' ')) },
    LogStyledMessage: message => logs.push(message),
    YG2Instance: (...args) => calls.push(args),
    ysdk: { isAvailableMethod: async () => true, leaderboards: {
      setScore: async (...args) => { submissions.push(args); },
      getPlayerEntry: async name => {
        verificationNames.push(name);
        assert.equal(calls.at(-1)[0], 'GameRatingSaved', 'A confirmed submission is acknowledged before diagnostic lookup');
        return { score: submissions.at(-1)[1], rank: 1 };
      },
      getDescription: async () => ({ default: true, description: { sort_order: 'DESC',
        score_format: { type: 'numeric', options: { decimal_offset: 0 } } } }),
      getEntries: async (name, options) => {
        queries.push({ name, options });
        return { entries: [{ rank: 1, score: 323, extraData: '{"waves":3,"kills":23}',
          player: { uniqueID: 'user-1', publicName: 'Тестовый игрок', getAvatarSrc: () => '' } }] };
      }
    } } });
  vm.runInContext(fs.readFileSync(root + 'Leaderboards/Scripts/Editor/CopyCode/LB_js.js', 'utf8'), ctx);
  await ctx.SetLeaderboard('mmr', 323, '{"waves":3,"kills":23}');
  assert.deepEqual(submissions[0], ['mmr', 323, '{"waves":3,"kills":23}']);
  assert.equal(calls.at(-1)[0], 'GameRatingSaved');
  assert.deepEqual(verificationNames, ['mmr']);
  assert(logs.includes('Leaderboard submission start: name=mmr score=323'));
  assert(logs.includes('Leaderboard submission saved: name=mmr score=323'));
  assert(logs.includes('Leaderboard stored entry: name=mmr score=323 rank=1'));
  ctx.ysdk.isAvailableMethod = async () => false;
  await ctx.SetLeaderboard('mmr', 324, '');
  assert.equal(submissions.length, 1, 'Unavailable submission does not overwrite score');
  assert.equal(calls.at(-1)[0], 'GameRatingFailed');
  ctx.ysdk.isAvailableMethod = async () => true;
  ctx.ysdk.leaderboards.getPlayerEntry = async () => {
    throw Object.assign(new Error('entry lookup failed'), { code: 'TEST_LOOKUP_FAILED' });
  };
  const beforeVerificationError = calls.length;
  await ctx.SetLeaderboard('mmr', 325, '');
  assert.deepEqual(calls.slice(beforeVerificationError), [['GameRatingSaved', '325']],
    'A failed verification does not emit GameRatingFailed after successful setScore');
  assert(warnings.some(message => message.includes('code=TEST_LOOKUP_FAILED message=entry lookup failed')));
  assert.equal(delayedChecks.length, 0, 'Unrelated lookup errors do not schedule another request');
  ctx.ysdk.leaderboards.getPlayerEntry = async () => {
    throw Object.assign(new Error('Player is not present in leaderboard'), { code: 'LEADERBOARD_PLAYER_NOT_PRESENT' });
  };
  await ctx.SetLeaderboard('mmr', 325, '');
  assert.equal(delayedChecks.length, 1);
  assert.equal(delayedChecks[0].delay, 60000);
  const writesBeforeDelay = submissions.length;
  ctx.ysdk.leaderboards.getPlayerEntry = async () => ({ score: 325, rank: 1 });
  await delayedChecks[0].callback();
  assert.equal(submissions.length, writesBeforeDelay, 'Delayed verification reads without rewriting score');
  assert(logs.includes('Leaderboard delayed stored entry: name=mmr score=325 rank=1'));
  ctx.ysdk.leaderboards.setScore = async () => ({ reason: 'TEST_SERVICE_ERROR', message: 'Rejected score' });
  const beforeResolvedError = calls.length;
  await ctx.SetLeaderboard('mmr', 326, '');
  assert.deepEqual(calls.slice(beforeResolvedError), [['GameRatingFailed']], 'Resolved platform error must not acknowledge a saved score');
  assert(errors.some(message => message.includes('code=TEST_SERVICE_ERROR')));
  ctx.ysdk.leaderboards.setScore = async () => {
    throw Object.assign(new Error('write failed'), { code: 'TEST_WRITE_FAILED' });
  };
  await ctx.SetLeaderboard('mmr', 326, '');
  assert.deepEqual(calls.at(-1), ['GameRatingFailed']);
  assert(errors.some(message => message.includes('code=TEST_WRITE_FAILED message=write failed')));
  ctx.GetLeaderboard('mmr', 15, 1, 'small', false);
  await new Promise(setImmediate);
  const rows = JSON.parse(calls.at(-1)[1]);
  assert.equal(rows.technoName, 'mmr');
  assert.equal(rows.type, 'numeric');
  assert.equal(rows.isInvertSortOrder, false);
  assert.equal(rows.names[0], 'Тестовый игрок');
  assert.equal(rows.extraDataArray[0], '{"waves":3,"kills":23}');
  assert(logs.includes('Leaderboard entries received: name=mmr count=1 ranks=1'));
  assert(!logs.join('\n').includes('Тестовый игрок') && !logs.join('\n').includes('user-1'),
    'Leaderboard diagnostics omit player names and unique IDs');
  // WebAssembly passes the C# bool argument to the .jslib bridge as numeric 1/0.
  for (const [auth, includeUser] of [[1, true], [0, false]]) {
    ctx.GetLeaderboard('mmr', 15, 1, 'small', auth);
    await new Promise(setImmediate);
    const query = queries.at(-1);
    assert.equal(query.name, 'mmr');
    assert.equal(query.options.includeUser, includeUser, 'SDK includeUser must be a strict boolean');
    assert.equal(query.options.quantityTop, 15);
    assert.equal(query.options.quantityAround, 1);
    assert.equal(calls.at(-1)[0], 'LeaderboardEntries');
    assert.equal(JSON.parse(calls.at(-1)[1]).ranks[0], 1);
  }
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
