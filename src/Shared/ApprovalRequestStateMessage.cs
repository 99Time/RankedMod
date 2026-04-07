namespace schrader
{
    public enum ApprovalRequestViewRole
    {
        None,
        CaptainDecision,
        RequesterStatus
    }

    public enum ApprovalRequestDisplayStatus
    {
        None,
        Pending,
        Approved,
        Rejected,
        Cooldown,
        Expired,
        Cancelled
    }

    public sealed class ApprovalRequestStateMessage
    {
        public bool IsVisible;
        public string RequestId;
        public ApprovalRequestViewRole ViewRole;
        public ApprovalRequestDisplayStatus Status;
        public string Title;
        public string PlayerName;
        public string PromptText;
        public string TargetTeamName;
        public string PreviousTeamName;
        public bool IsSwitchRequest;
        public string FooterText;
        public float SecondsRemaining;
        public int QueuePosition;
        public int QueueLength;

        public static ApprovalRequestStateMessage Hidden()
        {
            return new ApprovalRequestStateMessage
            {
                IsVisible = false,
                RequestId = string.Empty,
                ViewRole = ApprovalRequestViewRole.None,
                Status = ApprovalRequestDisplayStatus.None,
                Title = string.Empty,
                PlayerName = string.Empty,
                PromptText = string.Empty,
                TargetTeamName = string.Empty,
                PreviousTeamName = string.Empty,
                FooterText = string.Empty,
                SecondsRemaining = 0f,
                QueuePosition = 0,
                QueueLength = 0
            };
        }
    }
}