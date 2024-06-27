using System.Diagnostics.Eventing.Reader;

namespace ImageProjectBackend.Models;

public class ArchiveRequest
{
    public enum ArchiveStatus 
    {
        Failed = -1,
        Unknown,
        Pending,
        Canceled,
        Processing,
        Completed
    }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ArchiveStatus Status { get; set; } = ArchiveStatus.Unknown;
    public List<string> ExceptionMessages { get; set; } = new List<string>();

    public void AddError(string message) 
    {
        ExceptionMessages.Add(message);
        Status = ArchiveStatus.Failed;
    }
}


