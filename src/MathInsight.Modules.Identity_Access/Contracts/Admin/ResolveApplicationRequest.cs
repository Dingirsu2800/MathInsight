using System.ComponentModel.DataAnnotations;

namespace MathInsight.Modules.Identity_Access.Contracts.Admin;

public sealed class ResolveApplicationRequest
{
    /// <summary>true = Approve, false = Reject.</summary>
    [Required]
    public bool Approve { get; set; }

    [MaxLength(255)]
    public string? ReviewComments { get; set; }
}
