namespace QLStats.Data.Entities;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }

    public ICollection<ScoringRule> Rules { get; set; } = [];
}
