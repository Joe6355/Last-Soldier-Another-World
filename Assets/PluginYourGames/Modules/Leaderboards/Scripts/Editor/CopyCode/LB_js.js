async function SetLeaderboard(name, score, extraData) {
    try {
        if (!ysdk) throw new Error('SDK is unavailable');
        LogStyledMessage(`Leaderboard availability check: name=${name} score=${score}`);
        if (ysdk.isAvailableMethod && !(await ysdk.isAvailableMethod('leaderboards.setScore')))
            throw new Error('Leaderboard submission is unavailable');
        LogStyledMessage(`Leaderboard submission start: name=${name} score=${score}`);
        const result = await ysdk.leaderboards.setScore(name, score, extraData);
        // The platform can resolve a JSON error body instead of rejecting it.
        // Log only response structure and error tokens, never player data.
        const objectResult = result !== null && typeof result === 'object';
        const errorCode = objectResult ? result.code ?? result.reason ?? result.error?.code : undefined;
        const errorToken = typeof errorCode === 'string' ? errorCode.replace(/[^A-Za-z0-9_-]/g, '').slice(0, 96) : 'NONE';
        LogStyledMessage(`Leaderboard submission response: type=${typeof result} fields=${objectResult ? Object.keys(result).join(',') : ''} code=${errorToken}`);
        if (objectResult && (result.error || (result.reason && result.message))) {
            const error = new Error('Leaderboard service returned an error response');
            error.code = errorToken;
            throw error;
        }
        LogStyledMessage(`Leaderboard submission saved: name=${name} score=${score}`);
        YG2Instance('GameRatingSaved', String(score));
    } catch (e) {
        console.error('Set Leaderboard Error:', `code=${e?.code ?? 'UNKNOWN'} message=${e?.message ?? String(e)}`);
        YG2Instance('GameRatingFailed');
        return;
    }

    // This diagnostic read must never turn a successful submission into a failure.
    if (typeof ysdk?.leaderboards?.getPlayerEntry === 'function') {
        try {
            const entry = await ysdk.leaderboards.getPlayerEntry(name);
            LogStyledMessage(`Leaderboard stored entry: name=${name} score=${entry.score} rank=${entry.rank}`);
        } catch (e) {
            console.warn('Leaderboard stored entry check failed:', `code=${e?.code ?? 'UNKNOWN'} message=${e?.message ?? String(e)}`);
            if (e?.code === 'LEADERBOARD_PLAYER_NOT_PRESENT') {
                // Read again without another score write to distinguish a delayed
                // entry from an acknowledged write that did not persist.
                setTimeout(async () => {
                    try {
                        const entry = await ysdk.leaderboards.getPlayerEntry(name);
                        LogStyledMessage(`Leaderboard delayed stored entry: name=${name} score=${entry.score} rank=${entry.rank}`);
                    } catch (error) {
                        console.warn('Leaderboard delayed entry check failed:', `code=${error?.code ?? 'UNKNOWN'} message=${error?.message ?? String(error)}`);
                    }
                }, 60000);
            }
        }
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
                includeUser: Boolean(auth),
                quantityAround: quantityAround
            });
        })
        .then(res => {
            LogStyledMessage(`Leaderboard entries received: name=${nameLB} count=${res.entries.length} ranks=${res.entries.map(entry => entry.rank).join(',')}`);
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
