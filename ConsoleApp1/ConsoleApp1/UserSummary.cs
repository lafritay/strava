namespace ConsoleApp1
{
    public class UserSummary
    {
        public string User { get; set; }

        public int Count { get; set; }

        public long TotalTime { get; set; }

        public long TotalMovingTime { get; set; }

        public float DistanceRan { get; set; }

        public float DistanceBiked { get; set; }

        public float DistanceWalked { get; set; }

        public float TotalDistance()
        {
            return (float)(DistanceRan + DistanceWalked + (DistanceBiked / 3.5));
        }
    }
}