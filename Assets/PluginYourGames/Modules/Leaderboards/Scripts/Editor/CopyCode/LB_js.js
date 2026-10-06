async function SetLeaderboard(name, score, extraData) {
    try {
        if (!ysdk) throw new Error('SDK is unavailable');
        if (ysdk.isAvailableMethod && !(await ysdk.isAvailableMethod('leaderboards.setScore')))
            throw new Error('Leaderboard submission is unavailable');
        await ysdk.leaderboards.setScore(name, score, extraData);
        YG2Instance('GameRatingSaved', String(score));
    } catch (e) {
        console.error('Set Leaderboard Error:', e?.message ?? e);
        YG2Instance('GameRatingFailed');
    }
}

function GetLeaderboard(nameLB, quantityTop, quantityAround, photoSize, auth) {
    if (!ysdk) { YG2Instance('GameLeaderboardFailed', nameLB); return; }

    var jsonEntries = {
        technoName: '',
        isDefault: false,
        isInvertSortOrder: false,
        decimalOffset: 0,
        type: ''
    };

    ysdk.leaderboards.getDescription(nameLB)
        .then(res => {
            jsonEntries.technoName = nameLB;
            jsonEntries.isDefault = res.default;
            jsonEntries.isInvertSortOrder = res.description.sort_order === 'ASC' || res.description.invert_sort_order === true;
            jsonEntries.decimalOffset = res.description.score_format.options.decimal_offset;
            jsonEntries.type = res.description.score_format.type || res.description.type;

            return ysdk.leaderboards.getEntries(nameLB, {
                quantityTop: quantityTop,
                includeUser: auth,
                quantityAround: quantityAround
            });
        })
        .then(res => {
            let jsonPlayers = EntriesLB(res, photoSize);
            let combinedJson = { ...jsonEntries, ...jsonPlayers };

            YG2Instance('LeaderboardEntries', JSON.stringify(combinedJson));
        })
        .catch(err => {
            if (err.code === 'LEADERBOARD_PLAYER_NOT_PRESENT')
               LogStyledMessage('Leaderboard player not present');
            console.error(err);
            YG2Instance('GameLeaderboardFailed', nameLB);
        });
}

function EntriesLB(res, photoSize) {
    let LbdEntriesText = '';
    let plCount = res.entries.length;

    let ranks = new Array(plCount);
    let photos = new Array(plCount);
    let names = new Array(plCount);
    let scores = new Array(plCount);
    let uniqueIDs = new Array(plCount);
    let extraDataArray = new Array(plCount);

    for (let i = 0; i < plCount; i++) {
        ranks[i] = res.entries[i].rank;
        scores[i] = res.entries[i].score;
        uniqueIDs[i] = res.entries[i].player.uniqueID;
        photos[i] = res.entries[i].player.getAvatarSrc(photoSize);

        if (res.entries[i].extraData == "" || res.entries[i].extraData == null)
            extraDataArray[i] = NO_DATA;
        else
            extraDataArray[i] = res.entries[i].extraData;

        names[i] = res.entries[i].player.publicName || "anonymous";

        LbdEntriesText += ranks[i] + '. ' + names[i] + ": " + scores[i] + '\n';
    }

    if (plCount === 0) {
        LbdEntriesText = 'no data';
    }

    let jsonPlayers = {
        "entries": LbdEntriesText,
        "ranks": ranks,
        "photos": photos,
        "names": names,
        "scores": scores,
        "uniqueIDs": uniqueIDs,
        "extraDataArray": extraDataArray
    };

    return jsonPlayers;
}
