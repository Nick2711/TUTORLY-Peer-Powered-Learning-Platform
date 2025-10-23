using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Tutorly.Server.DatabaseModels
{
    [Table("forum_reports")]
    public class ForumReportEntity : BaseModel
    {
        [PrimaryKey("report_id", false)]
        public int ReportId { get; set; }

        [Column("reported_by_user_id")]
        public string ReportedByUserId { get; set; } = string.Empty;

        [Column("reported_by_tutor_id")]
        public int? ReportedByTutorId { get; set; }

        [Column("reported_by_student_id")]
        public int? ReportedByStudentId { get; set; }

        [Column("report_type")]
        public string ReportType { get; set; } = "post"; // post, response, resource

        [Column("reported_item_id")]
        public int ReportedItemId { get; set; }

        [Column("reason")]
        public string Reason { get; set; } = string.Empty;

        [Column("details")]
        public string? Details { get; set; }

        [Column("severity")]
        public string Severity { get; set; } = "mild"; // mild, moderate, severe

        [Column("status")]
        public string Status { get; set; } = "open"; // open, resolved, dismissed

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [Column("resolved_by_user_id")]
        public string? ResolvedByUserId { get; set; }

        [Column("resolution_notes")]
        public string? ResolutionNotes { get; set; }

        // Computed property for reporter name
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Newtonsoft.Json.JsonIgnore]
        public string ReporterName => ReportedByTutorId?.ToString() ?? ReportedByStudentId?.ToString() ?? ReportedByUserId;
    }
}
