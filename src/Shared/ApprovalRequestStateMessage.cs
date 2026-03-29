namespace schrader
{
    public sealed class ApprovalRequestStateMessage
    {
        public bool IsVisible;
        public string RequestId;
        public string Title;
        public string PlayerName;
        public string PromptText;
        public string TargetTeamName;
        public string PreviousTeamName;
        public bool IsSwitchRequest;
        public string FooterText;

        public static ApprovalRequestStateMessage Hidden()
        {
            return new ApprovalRequestStateMessage
            {
                IsVisible = false,
                RequestId = string.Empty,
                Title = string.Empty,
                PlayerName = string.Empty,
                PromptText = string.Empty,
                TargetTeamName = string.Empty,
                PreviousTeamName = string.Empty,
                FooterText = string.Empty
            };
        }
    }
}