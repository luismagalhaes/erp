using System.ComponentModel.DataAnnotations;

namespace Erp.Notification.Domain.Models;

public sealed class EmailNotificationRequest
{
    [Required]
    [EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string HtmlBody { get; set; } = string.Empty;
}
