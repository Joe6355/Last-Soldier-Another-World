var cloudSaves = NO_DATA;
var gameCloudStatus = 0;

async function SaveCloud(jsonData, flush) {
    try {
        if (!player || gameCloudStatus !== 1) throw new Error('Cloud storage is unavailable');
        await player.setData({ saves: [jsonData] }, Boolean(flush));
        const data = JSON.parse(jsonData);
        YG2Instance('GameCloudSaved', JSON.stringify({ ownerId: data.ownerId, idSave: data.idSave }));
    } catch (e) {
        console.error('Save Cloud Error:', e?.message ?? e);
        YG2Instance('GameCloudFailed');
    }
}

async function LoadCloud() {
    let result = NO_DATA;
    gameCloudStatus = -2;
    try {
        if (!ysdk || !player) throw new Error('Player is unavailable');
        const data = await new Promise((resolve, reject) => {
            const timer = setTimeout(() => reject(new Error('Cloud load timed out')), 25000);
            Promise.resolve(player.getData(['saves'])).then(value => {
                clearTimeout(timer);
                resolve(value);
            }, error => {
                clearTimeout(timer);
                reject(error);
            });
        });
        if (data.saves) {
            if (!Array.isArray(data.saves) || data.saves.length !== 1 || typeof data.saves[0] !== 'string')
                throw new Error('Unexpected cloud save format');
            const save = JSON.parse(data.saves[0]);
            if (!save || save.progressVersion !== 1 || typeof save.ownerId !== 'string')
                throw new Error('Unsupported cloud save schema');
            result = JSON.stringify(data.saves);
        }
        gameCloudStatus = 1;
    } catch (e) {
        console.error('Load Cloud Error:', e?.message ?? e);
        gameCloudStatus = -1;
    }
    cloudSaves = result;
    YG2Instance('SetLoadSaves', result);
    return result;
}
