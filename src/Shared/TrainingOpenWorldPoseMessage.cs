namespace schrader
{
    internal sealed class TrainingOpenWorldPoseMessage
    {
        public bool IsOpenWorldActive { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float RotationEulerX { get; set; }
        public float RotationEulerY { get; set; }
        public float RotationEulerZ { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}