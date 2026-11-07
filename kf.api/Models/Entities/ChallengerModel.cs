using System.ComponentModel.DataAnnotations;

namespace KnightFight.Api.Models.Entities;

public class ChallengerModel : BaseModel
{
    [MaxLength(200)]
    public required string ChallengerName { get; set; }
    
    [MaxLength(500)]
    public required string Secret { get; set; }
    
    public int Challenges { get; set; } = 0;
    
    public int Wins { get; set; } = 0;
    
    public int Loses { get; set; } = 0;
    
    public int Draws { get; set; } = 0;
    
    /// <summary>
    /// The challenger's own avatar, already resized to 50x50 and stored as PNG.
    /// Null when the challenger uses one of the bundled default avatars.
    /// </summary>
    public byte[]? AvatarBlob { get; set; }
    
    /// <summary>
    /// Index into the ten bundled default avatars (0..9).
    /// Null when the challenger has a custom <see cref="AvatarBlob"/>.
    /// </summary>
    public int? DefaultAvatarIndex { get; set; }
}