let stickyRequested = false;
let stickyApplying = false;

async function StickyAdActivity(show) {
    stickyRequested = Boolean(show);
    if (stickyApplying || !ysdk || !ysdk.adv) return;
    stickyApplying = true;
    try {
        let applied;
        do {
            applied = stickyRequested;
            if (applied) await ysdk.adv.showBannerAdv();
            else await ysdk.adv.hideBannerAdv();
        } while (applied !== stickyRequested);
    } catch (error) { console.warn('Sticky advertisement unavailable', error); }
    finally { stickyApplying = false; }
}
