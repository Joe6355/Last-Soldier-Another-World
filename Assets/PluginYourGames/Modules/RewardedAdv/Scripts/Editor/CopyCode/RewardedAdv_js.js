function RewardedAdvShow(id) {
    let finished = false;
    let rewarded = false;
    const close = (error) => {
        if (finished) return;
        finished = true;
        if (error) {
            console.warn('Rewarded advertisement unavailable', error);
            YG2Instance('ErrorRewardedAdv');
        }
        YG2Instance('CloseRewardedAdv');
        FocusGame();
    };
    try {
        if (!ysdk || !ysdk.adv) throw new Error('SDK is unavailable');
        ysdk.adv.showRewardedVideo({ callbacks: {
            onOpen: () => { if (!finished) YG2Instance('OpenRewardedAdv'); },
            onRewarded: () => {
                if (finished || rewarded) return;
                rewarded = true;
                YG2Instance('RewardAdv', id);
            },
            onClose: () => close(),
            onError: (error) => close(error)
        }});
    } catch (error) { close(error); }
}
