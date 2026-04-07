namespace schrader.Server
{
    internal enum BotPlayStyle
    {
        Skater = 0,
        Goalie = 1
    }

    internal enum BotGoalieDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    internal struct BotGoalieSettings
    {
        public float DashCooldown;
        public float DashCancelGrace;
        public float DashThreshold;
        public float CancelThreshold;
        public float ReactionTime;
        public float MaxRotationAngle;
        public float RotationSpeed;
        public float DistanceFromNet;

        public static BotGoalieSettings Create(BotGoalieDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotGoalieDifficulty.Easy:
                    return new BotGoalieSettings
                    {
                        DashCooldown = 1f,
                        DashCancelGrace = 0.25f,
                        DashThreshold = 1f,
                        CancelThreshold = 0.08f,
                        ReactionTime = 0.25f,
                        MaxRotationAngle = 30f,
                        RotationSpeed = 6f,
                        DistanceFromNet = 1f
                    };

                case BotGoalieDifficulty.Normal:
                    return new BotGoalieSettings
                    {
                        DashCooldown = 0.6f,
                        DashCancelGrace = 0.15f,
                        DashThreshold = 0.4f,
                        CancelThreshold = 0.05f,
                        ReactionTime = 0.15f,
                        MaxRotationAngle = 75f,
                        RotationSpeed = 12f,
                        DistanceFromNet = 1.2f
                    };

                default:
                    return new BotGoalieSettings
                    {
                        DashCooldown = 0.2f,
                        DashCancelGrace = 0.15f,
                        DashThreshold = 0.2f,
                        CancelThreshold = 0.05f,
                        ReactionTime = 0.15f,
                        MaxRotationAngle = 85f,
                        RotationSpeed = 18f,
                        DistanceFromNet = 1.4f
                    };
            }
        }
    }
}