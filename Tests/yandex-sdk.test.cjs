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

(async () => {
  await cloudChecks();
  await authChecks();
  console.log('PASS: cloud round trip, rejected save, network failure protection, corrupt data, empty account, auth cancellation');
})().catch(error => { console.error(error); process.exitCode = 1; });
