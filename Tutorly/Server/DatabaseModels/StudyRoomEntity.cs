using System.ComponentModel.DataAnnotations;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels;

[Supabase.Postgrest.Attributes.Table("study_rooms")]
public class StudyRoomEntity : BaseModel
{
    [PrimaryKey("room_id", false)]
    [Supabase.Postgrest.Attributes.Column("room_id")]
    public Guid RoomId { get; set; }

    [Supabase.Postgrest.Attributes.Column("room_name")]
    public string RoomName { get; set; } = string.Empty;

    [Supabase.Postgrest.Attributes.Column("description")]
    public string? Description { get; set; }

    [Supabase.Postgrest.Attributes.Column("creator_user_id")]
    public Guid CreatorUserId { get; set; }

    [Supabase.Postgrest.Attributes.Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Supabase.Postgrest.Attributes.Column("scheduled_start_time")]
    public DateTime? ScheduledStartTime { get; set; }

    [Supabase.Postgrest.Attributes.Column("scheduled_end_time")]
    public DateTime? ScheduledEndTime { get; set; }

    [Supabase.Postgrest.Attributes.Column("room_type")]
    public string RoomType { get; set; } = "Standalone"; // Standalone, StudentSession, TutorSession

    [Supabase.Postgrest.Attributes.Column("privacy")]
    public string Privacy { get; set; } = "Public"; // Public, ModuleSpecific, PrivateInviteOnly

    [Supabase.Postgrest.Attributes.Column("module_id")]
    public Guid? ModuleId { get; set; }

    [Supabase.Postgrest.Attributes.Column("max_participants")]
    public int MaxParticipants { get; set; } = 10;

    [Supabase.Postgrest.Attributes.Column("status")]
    public string Status { get; set; } = "Scheduled"; // Scheduled, Active, Ended

    [Supabase.Postgrest.Attributes.Column("room_code")]
    public string? RoomCode { get; set; }

    [Supabase.Postgrest.Attributes.Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public enum RoomType
{
    Standalone,
    StudentSession,
    TutorSession
}

public enum RoomPrivacy
{
    Public,
    ModuleSpecific,
    PrivateInviteOnly
}

public enum RoomStatus
{
    Scheduled,
    Active,
    Ended
}