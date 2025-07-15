using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models.DTOs;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models.DTOs
{
    /// <summary>
    /// DTO for Employee Sync Status requests and responses
    /// </summary>
    public class EmployeeSyncStatusDto
    {
        [JsonPropertyName("personnel_id")]
        public int PersonnelId { get; set; }

        [JsonPropertyName("personnel_number")]
        public string? PersonnelNumber { get; set; }

        [JsonPropertyName("last_sync_datetime")]
        public DateTime? LastSyncDateTime { get; set; }

        [JsonPropertyName("sync_status")]
        [Required]
        [StringLength(20)]
        public string SyncStatus { get; set; } = "never_synced";

        [JsonPropertyName("telp_last_modified")]
        public DateTime? TelpLastModified { get; set; }

        [JsonPropertyName("lynks_last_modified")]
        public DateTime? LynksLastModified { get; set; }

        [JsonPropertyName("failed_fields")]
        public List<string>? FailedFields { get; set; }

        [JsonPropertyName("sync_attempts")]
        public int SyncAttempts { get; set; }

        [JsonPropertyName("last_error_message")]
        public string? LastErrorMessage { get; set; }
    }

    /// <summary>
    /// DTO for creating/updating Employee Sync Status
    /// </summary>
    public class CreateEmployeeSyncStatusDto
    {
        [JsonPropertyName("personnel_id")]
        [Required]
        public int PersonnelId { get; set; }

        [JsonPropertyName("sync_status")]
        [Required]
        [StringLength(20)]
        public string SyncStatus { get; set; } = "never_synced";

        [JsonPropertyName("telp_last_modified")]
        public DateTime? TelpLastModified { get; set; }

        [JsonPropertyName("lynks_last_modified")]
        public DateTime? LynksLastModified { get; set; }

        [JsonPropertyName("failed_fields")]
        public List<string>? FailedFields { get; set; }

        [JsonPropertyName("last_error_message")]
        public string? LastErrorMessage { get; set; }
    }

    /// <summary>
    /// DTO for marking sync as successful
    /// </summary>
    public class MarkSyncSuccessfulDto
    {
        [JsonPropertyName("personnel_id")]
        [Required]
        public int PersonnelId { get; set; }

        [JsonPropertyName("telp_last_modified")]
        public DateTime? TelpLastModified { get; set; }

        [JsonPropertyName("lynks_last_modified")]
        public DateTime? LynksLastModified { get; set; }
    }

    /// <summary>
    /// DTO for marking sync as failed
    /// </summary>
    public class MarkSyncFailedDto
    {
        [JsonPropertyName("personnel_id")]
        [Required]
        public int PersonnelId { get; set; }

        [JsonPropertyName("error_message")]
        [Required]
        [StringLength(2000)]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonPropertyName("failed_fields")]
        public List<string>? FailedFields { get; set; }
    }

    /// <summary>
    /// DTO for bulk sync status response
    /// </summary>
    public class BulkSyncStatusResponseDto
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("synced_count")]
        public int SyncedCount { get; set; }

        [JsonPropertyName("failed_count")]
        public int FailedCount { get; set; }

        [JsonPropertyName("pending_count")]
        public int PendingCount { get; set; }

        [JsonPropertyName("never_synced_count")]
        public int NeverSyncedCount { get; set; }

        [JsonPropertyName("sync_statuses")]
        public List<EmployeeSyncStatusDto> SyncStatuses { get; set; } = new List<EmployeeSyncStatusDto>();
    }

    /// <summary>
    /// DTO for sync statistics
    /// </summary>
    public class SyncStatisticsDto
    {
        [JsonPropertyName("total_personnel")]
        public int TotalPersonnel { get; set; }

        [JsonPropertyName("total_with_sync_status")]
        public int TotalWithSyncStatus { get; set; }

        [JsonPropertyName("synced_count")]
        public int SyncedCount { get; set; }

        [JsonPropertyName("failed_count")]
        public int FailedCount { get; set; }

        [JsonPropertyName("pending_count")]
        public int PendingCount { get; set; }

        [JsonPropertyName("never_synced_count")]
        public int NeverSyncedCount { get; set; }

        [JsonPropertyName("sync_success_rate")]
        public decimal SyncSuccessRate { get; set; }

        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; }
    }
}

/// <summary>
/// Extension methods for converting between Entity and DTO
/// </summary>
public static class EmployeeSyncStatusExtensions
{
    /// <summary>
    /// Convert EmployeeSyncStatus entity to DTO
    /// </summary>
    public static EmployeeSyncStatusDto ToDto(this EmployeeSyncStatus entity)
    {
        List<string>? failedFields = null;

        if (!string.IsNullOrEmpty(entity.failed_fields))
        {
            try
            {
                failedFields = System.Text.Json.JsonSerializer.Deserialize<List<string>>(entity.failed_fields);
            }
            catch (System.Text.Json.JsonException)
            {
                // If JSON parsing fails, treat as single field
                failedFields = new List<string> { entity.failed_fields };
            }
        }

        return new EmployeeSyncStatusDto
        {
            PersonnelId = entity.personnel_id,
            PersonnelNumber = entity.Personnel?.personnel_number,
            LastSyncDateTime = entity.last_sync_datetime,
            SyncStatus = entity.sync_status,
            TelpLastModified = entity.telp_last_modified,
            LynksLastModified = entity.lynks_last_modified,
            FailedFields = failedFields,
            SyncAttempts = entity.sync_attempts,
            LastErrorMessage = entity.last_error_message
        };
    }

    /// <summary>
    /// Convert CreateEmployeeSyncStatusDto to entity
    /// </summary>
    public static EmployeeSyncStatus ToEntity(this CreateEmployeeSyncStatusDto dto)
    {
        string? failedFieldsJson = null;

        if (dto.FailedFields != null && dto.FailedFields.Any())
        {
            failedFieldsJson = System.Text.Json.JsonSerializer.Serialize(dto.FailedFields);
        }

        return new EmployeeSyncStatus
        {
            personnel_id = dto.PersonnelId,
            sync_status = dto.SyncStatus,
            telp_last_modified = dto.TelpLastModified,
            lynks_last_modified = dto.LynksLastModified,
            failed_fields = failedFieldsJson,
            last_error_message = dto.LastErrorMessage,
            sync_attempts = 0
        };
    }

    /// <summary>
    /// Convert collection of entities to DTOs
    /// </summary>
    public static List<EmployeeSyncStatusDto> ToDtoList(this IEnumerable<EmployeeSyncStatus> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
}