let nowFullAdOpen = false;

function InterAdvShow() {
    if (nowFullAdOpen) return;
    nowFullAdOpen = true;
    let finished = false;
    const close = (wasShown, error) => {
        if (finished) return;
        finished = true;
        nowFullAdOpen = false;
        if (error) {
            console.warn('Interstitial advertisement unavailable', error);
            YG2Instance('ErrorInterAdv');
        }
        if (initGame === true) YG2Instance('CloseInterAdv', wasShown ? 'true' : 'false');
        FocusGame();
    };
    try {
        if (!ysdk || !ysdk.adv) throw new Error('SDK is unavailable');
        ysdk.adv.showFullscreenAdv({ callbacks: {
            onOpen: () => { if (!finished && initGame === true) YG2Instance('OpenInterAdv'); },
            onClose: (wasShown) => close(wasShown),
            onError: (error) => close(false, error)
        }});
    } catch (error) { close(false, error); }
}
