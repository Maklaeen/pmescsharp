namespace PmesCSharp.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public string? ErrorType { get; set; }  // "db", "timeout", "notfound", "denied", "session", "offline", "save", null
    public string? Title { get; set; }
    public string? Message { get; set; }
}
