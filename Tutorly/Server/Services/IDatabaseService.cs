using Tutorly.Shared;

namespace Tutorly.Server.Services
{
    public interface IDatabaseService
    {
        Task<ServiceResult<List<DatabaseTableInfo>>> GetAllTablesAsync();
        Task<ServiceResult<DatabaseTableData>> GetTableDataAsync(string tableName, int page = 1, int pageSize = 10, string searchQuery = "");
        Task<ServiceResult<DatabaseRecord>> GetRecordAsync(string tableName, string primaryKeyValue);
        Task<ServiceResult<DatabaseRecord>> UpdateRecordAsync(string tableName, string primaryKeyValue, Dictionary<string, object> updates);
        Task<ServiceResult> DeleteRecordAsync(string tableName, string primaryKeyValue);
        Task<ServiceResult<List<DatabaseErrorLog>>> GetErrorLogsAsync(int page = 1, int pageSize = 10, string typeFilter = "0", bool onlyUnresolved = false);
        Task<ServiceResult> MarkErrorResolvedAsync(int errorId);
        Task<ServiceResult> DeleteErrorLogAsync(int errorId);
        Task<ServiceResult> MarkAllErrorsResolvedAsync();
        Task<ServiceResult<string>> ExportTableToCsvAsync(string tableName);
        Task<ServiceResult> ImportCsvToTableAsync(string tableName, string csvContent);
        Task<ServiceResult<DatabaseStats>> GetDatabaseStatsAsync();
    }
}
