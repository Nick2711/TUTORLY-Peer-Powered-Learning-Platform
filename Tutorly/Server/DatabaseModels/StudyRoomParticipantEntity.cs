using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Supabase.Postgrest.Attributes.Table("study_room_participants")]
public class StudyRoomParticipantEntity : BaseModel
{
    [PrimaryKey("id", false)]
    [Supabase.Postgrest.Attributes.Column("id")]
    public Guid Id { get; set; }

    [Supabase.Postgrest.Attributes.Column("room_id")]
    public Guid RoomId { get; set; }

    [Supabase.Postgrest.Attributes.Column("user_id")]
    public Guid UserId { get; set; }

    [Supabase.Postgrest.Attributes.Column("user_name")]
    public string UserName { get; set; } = string.Empty;

    [Supabase.Postgrest.Attributes.Column("user_email")]
    public string? UserEmail { get; set; }

    [Supabase.Postgrest.Attributes.Column("user_role")]
    public string? UserRole { get; set; } // Student, Tutor, Admin

    [Supabase.Postgrest.Attributes.Column("connection_id")]
    public string? ConnectionId { get; set; }

    [Supabase.Postgrest.Attributes.Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [Supabase.Postgrest.Attributes.Column("left_at")]
    public DateTime? LeftAt { get; set; }

    [Supabase.Postgrest.Attributes.Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Supabase.Postgrest.Attributes.Column("is_muted")]
    public bool IsMuted { get; set; } = false;

    [Supabase.Postgrest.Attributes.Column("is_screen_sharing")]
    public bool IsScreenSharing { get; set; } = false;

    [Supabase.Postgrest.Attributes.Column("is_video_enabled")]
    public bool IsVideoEnabled { get; set; } = true;

    [Supabase.Postgrest.Attributes.Column("last_seen")]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}