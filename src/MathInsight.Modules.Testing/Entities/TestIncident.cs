namespace MathInsight.Modules.Testing.Entities;

public class TestIncident
{
    public string IncidentId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // TAB_SWITCH, FOCUS_LOSS
    public DateTime Time { get; set; }

    // Navigation
    public TestSession? Session { get; set; }
}
