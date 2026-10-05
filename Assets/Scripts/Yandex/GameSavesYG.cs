namespace YG
{
    public partial class SavesYG
    {
        public int progressVersion;
        public string ownerId;
        public bool cloudBaseKnown;
    }
}

namespace YG.Insides
{
    public partial class YGSendMessage
    {
        public void GameCloudSaved(string receipt) => GameProgress.CloudSaved(receipt);
        public void GameCloudFailed() => GameProgress.CloudSaveFailed();
        public void GameAuthClosed() => GameProgress.AuthorizationClosed();
        public void GameRatingSaved(string score) => GameProgress.RatingSaved(score);
        public void GameRatingFailed() => GameProgress.RatingFailed();
    }
}
