namespace ZippingWorker_Service.Model
{
    public class ZipValidation
    {
        public DateTime StartTime { get; set; } = DateTime.MinValue;
        public DateTime FinishTime { get; set; } = DateTime.MinValue;
        public bool Success { get; set; } = false;
        public double Duration => (FinishTime - StartTime).TotalSeconds;
    }
}
