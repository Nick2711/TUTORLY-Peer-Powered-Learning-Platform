using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tutorly.Server.Services;
using Tutorly.Shared;

namespace Tutorly.Server.Controller
{
    [ApiController]
    [Route("api/admin/database")]
    [Authorize]
    public class DatabaseController : ControllerBase
    {
        private readonly IDatabaseService _databaseService;
        private readonly ILogger<DatabaseController> _logger;

        public DatabaseController(IDatabaseService databaseService, ILogger<DatabaseController> logger)
        {
            _databaseService = databaseService;
            _logger = logger;
        }

        /// <summary>
        /// Get all database tables
        /// </summary>
        /// <returns>List of database tables</returns>
        [HttpGet("tables")]
        public async Task<IActionResult> GetAllTables()
        {
            try
            {
                _logger.LogInformation("🔍 GetAllTables endpoint called");

                var result = await _databaseService.GetAllTablesAsync();

                if (result.Success)
                {
                    _logger.LogInformation("✅ Retrieved {Count} tables", result.Data.Count);
                    return Ok(new { message = "Tables retrieved successfully", tables = result.Data });
                }

                _logger.LogWarning("❌ Failed to get tables: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetAllTables endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get table data with pagination and search
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <param name="searchQuery">Search query (optional)</param>
        /// <returns>Table data</returns>
        [HttpGet("tables/{tableName}/data")]
        public async Task<IActionResult> GetTableData(string tableName, int page = 1, int pageSize = 10, string searchQuery = "")
        {
            try
            {
                _logger.LogInformation("🔍 GetTableData endpoint called for table {TableName}, page {Page}", tableName, page);

                var result = await _databaseService.GetTableDataAsync(tableName, page, pageSize, searchQuery);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Retrieved {RecordCount} records for table {TableName}", result.Data.Records.Count, tableName);
                    return Ok(new { message = "Table data retrieved successfully", data = result.Data });
                }

                _logger.LogWarning("❌ Failed to get table data: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetTableData endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get a specific record from a table
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <param name="primaryKeyValue">Primary key value</param>
        /// <returns>Record data</returns>
        [HttpGet("tables/{tableName}/records/{primaryKeyValue}")]
        public async Task<IActionResult> GetRecord(string tableName, string primaryKeyValue)
        {
            try
            {
                _logger.LogInformation("🔍 GetRecord endpoint called for table {TableName}, key {PrimaryKeyValue}", tableName, primaryKeyValue);

                var result = await _databaseService.GetRecordAsync(tableName, primaryKeyValue);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Retrieved record {PrimaryKeyValue} from table {TableName}", primaryKeyValue, tableName);
                    return Ok(new { message = "Record retrieved successfully", record = result.Data });
                }

                _logger.LogWarning("❌ Failed to get record: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetRecord endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Update a record in a table
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <param name="primaryKeyValue">Primary key value</param>
        /// <param name="request">Update request</param>
        /// <returns>Updated record</returns>
        [HttpPut("tables/{tableName}/records/{primaryKeyValue}")]
        public async Task<IActionResult> UpdateRecord(string tableName, string primaryKeyValue, [FromBody] RecordUpdateRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 UpdateRecord endpoint called for table {TableName}, key {PrimaryKeyValue}", tableName, primaryKeyValue);

                if (request == null || request.Updates == null)
                {
                    return BadRequest(new { error = "Update data is required" });
                }

                var result = await _databaseService.UpdateRecordAsync(tableName, primaryKeyValue, request.Updates);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Updated record {PrimaryKeyValue} in table {TableName}", primaryKeyValue, tableName);
                    return Ok(new { message = "Record updated successfully", record = result.Data });
                }

                _logger.LogWarning("❌ Failed to update record: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in UpdateRecord endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a record from a table
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <param name="primaryKeyValue">Primary key value</param>
        /// <returns>Success message</returns>
        [HttpDelete("tables/{tableName}/records/{primaryKeyValue}")]
        public async Task<IActionResult> DeleteRecord(string tableName, string primaryKeyValue)
        {
            try
            {
                _logger.LogInformation("🔍 DeleteRecord endpoint called for table {TableName}, key {PrimaryKeyValue}", tableName, primaryKeyValue);

                var result = await _databaseService.DeleteRecordAsync(tableName, primaryKeyValue);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Deleted record {PrimaryKeyValue} from table {TableName}", primaryKeyValue, tableName);
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Failed to delete record: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DeleteRecord endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get database error logs
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 10)</param>
        /// <param name="typeFilter">Error type filter (default: "0" for all)</param>
        /// <param name="onlyUnresolved">Only show unresolved errors (default: false)</param>
        /// <returns>Error logs</returns>
        [HttpGet("error-logs")]
        public async Task<IActionResult> GetErrorLogs(int page = 1, int pageSize = 10, string typeFilter = "0", bool onlyUnresolved = false)
        {
            try
            {
                _logger.LogInformation("🔍 GetErrorLogs endpoint called, page {Page}", page);

                var result = await _databaseService.GetErrorLogsAsync(page, pageSize, typeFilter, onlyUnresolved);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Retrieved {Count} error logs", result.Data.Count);
                    return Ok(new { message = "Error logs retrieved successfully", logs = result.Data });
                }

                _logger.LogWarning("❌ Failed to get error logs: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetErrorLogs endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark an error as resolved
        /// </summary>
        /// <param name="errorId">Error ID</param>
        /// <returns>Success message</returns>
        [HttpPut("error-logs/{errorId}/resolve")]
        public async Task<IActionResult> MarkErrorResolved(int errorId)
        {
            try
            {
                _logger.LogInformation("🔍 MarkErrorResolved endpoint called for error {ErrorId}", errorId);

                var result = await _databaseService.MarkErrorResolvedAsync(errorId);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Marked error {ErrorId} as resolved", errorId);
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Failed to mark error as resolved: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in MarkErrorResolved endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete an error log
        /// </summary>
        /// <param name="errorId">Error ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("error-logs/{errorId}")]
        public async Task<IActionResult> DeleteErrorLog(int errorId)
        {
            try
            {
                _logger.LogInformation("🔍 DeleteErrorLog endpoint called for error {ErrorId}", errorId);

                var result = await _databaseService.DeleteErrorLogAsync(errorId);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Deleted error log {ErrorId}", errorId);
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Failed to delete error log: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DeleteErrorLog endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Mark all errors as resolved
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPut("error-logs/resolve-all")]
        public async Task<IActionResult> MarkAllErrorsResolved()
        {
            try
            {
                _logger.LogInformation("🔍 MarkAllErrorsResolved endpoint called");

                var result = await _databaseService.MarkAllErrorsResolvedAsync();

                if (result.Success)
                {
                    _logger.LogInformation("✅ Marked all errors as resolved");
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Failed to mark all errors as resolved: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in MarkAllErrorsResolved endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Export table to CSV
        /// </summary>
        /// <param name="tableName">Name of the table</param>
        /// <returns>CSV content</returns>
        [HttpGet("tables/{tableName}/export")]
        public async Task<IActionResult> ExportTable(string tableName)
        {
            try
            {
                _logger.LogInformation("🔍 ExportTable endpoint called for table {TableName}", tableName);

                var result = await _databaseService.ExportTableToCsvAsync(tableName);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Exported table {TableName} to CSV", tableName);
                    return File(System.Text.Encoding.UTF8.GetBytes(result.Data), "text/csv", $"{tableName}.csv");
                }

                _logger.LogWarning("❌ Failed to export table: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in ExportTable endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Import CSV to table
        /// </summary>
        /// <param name="request">Import request</param>
        /// <returns>Success message</returns>
        [HttpPost("tables/import")]
        public async Task<IActionResult> ImportCsv([FromBody] CsvImportRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 ImportCsv endpoint called for table {TableName}", request.TableName);

                if (request == null || string.IsNullOrWhiteSpace(request.TableName) || string.IsNullOrWhiteSpace(request.CsvContent))
                {
                    return BadRequest(new { error = "Table name and CSV content are required" });
                }

                var result = await _databaseService.ImportCsvToTableAsync(request.TableName, request.CsvContent);

                if (result.Success)
                {
                    _logger.LogInformation("✅ Imported CSV to table {TableName}", request.TableName);
                    return Ok(new { message = result.Message });
                }

                _logger.LogWarning("❌ Failed to import CSV: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in ImportCsv endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        /// <returns>Database statistics</returns>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDatabaseStats()
        {
            try
            {
                _logger.LogInformation("🔍 GetDatabaseStats endpoint called");

                var result = await _databaseService.GetDatabaseStatsAsync();

                if (result.Success)
                {
                    _logger.LogInformation("✅ Retrieved database statistics");
                    return Ok(new { message = "Database statistics retrieved successfully", stats = result.Data });
                }

                _logger.LogWarning("❌ Failed to get database statistics: {Message}", result.Message);
                return BadRequest(new { error = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in GetDatabaseStats endpoint: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
