namespace ResonanceTracker
{
    public class ResonanceTrackerConfig
    {
        public string OwnFillColorHex { get; set; } = "#FFFFFF";
        public string OwnBorderColorHex { get; set; } = "#000000";
        public string OtherFillColorHex { get; set; } = "#C0C0C0";
        public string OtherBorderColorHex { get; set; } = "#4D4D4D";
        public bool UsePlayerCustomColors { get; set; } = true;
    }
}
