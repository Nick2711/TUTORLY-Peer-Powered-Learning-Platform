using System.Text.Json.Serialization;

namespace Tutorly.Shared
{
    public class DatabaseTableInfo
    {
        public string TableName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Size { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public string PrimaryKey { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }

    public class DatabaseTableData
    {
        public string TableName { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public List<DatabaseRecord> Records { get; set; } = new();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string PrimaryKey { get; set; } = string.Empty;
    }

    public class DatabaseRecord
    {
        public Dictionary<string, object> Data { get; set; } = new();
        public string PrimaryKeyValue { get; set; } = string.Empty;
    }

    public class DatabaseErrorLog
    {
        public int ErrorId { get; set; }
        public int ErrorLine { get; set; }
        public int ErrorType { get; set; }
        public string ErrorDescription { get; set; } = string.Empty;
        public bool ErrorResolved { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class DatabaseStats
    {
        public int TotalTables { get; set; }
        public long TotalRecords { get; set; }
        public string DatabaseSize { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public int ErrorCount { get; set; }
        public int UnresolvedErrors { get; set; }
        public DateTime LastBackup { get; set; }
        public List<TableStats> TableStats { get; set; } = new();
    }

    public class TableStats
    {
        public string TableName { get; set; } = string.Empty;
        public long RowCount { get; set; }
        public string Size { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }

    public class CsvImportRequest
    {
        public string TableName { get; set; } = string.Empty;
        public string CsvContent { get; set; } = string.Empty;
        public bool HasHeader { get; set; } = true;
    }

    public class RecordUpdateRequest
    {
        public Dictionary<string, object> Updates { get; set; } = new();
    }
}
