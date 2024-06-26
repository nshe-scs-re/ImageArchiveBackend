namespace ImageProjectBackend.Models;

public class ArchiveRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCompleted { get; set; } = false;
}
