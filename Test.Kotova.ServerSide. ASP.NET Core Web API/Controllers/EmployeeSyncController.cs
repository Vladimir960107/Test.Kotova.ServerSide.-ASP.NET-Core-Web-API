using Microsoft.AspNetCore.Mvc;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models.DTOs;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeSyncController : ControllerBase
    {
        private readonly IEmployeeSyncService _syncService;
        private readonly ILynksDbService _lynksDbService;

        public EmployeeSyncController(IEmployeeSyncService syncService, ILynksDbService lynksDbService)
        {
            _syncService = syncService;
            _lynksDbService = lynksDbService;
        }

        /// <summary>
        /// Get sync status for a specific personnel
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>Employee sync status</returns>
        [HttpGet("{personnelId}")]
        public async Task<ActionResult<EmployeeSyncStatusDto>> GetSyncStatus(int personnelId)
        {
            try
            {
                var syncStatus = await _syncService.GetSyncStatusAsync(personnelId);

                if (syncStatus == null)
                {
                    return NotFound($"Sync status for personnel ID {personnelId} not found");
                }

                return Ok(syncStatus.ToDto());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all sync statuses with optional filtering
        /// </summary>
        /// <param name="status">Optional filter by sync status</param>
        /// <returns>List of employee sync statuses</returns>
        [HttpGet]
        public async Task<ActionResult<BulkSyncStatusResponseDto>> GetAllSyncStatuses([FromQuery] string? status = null)
        {
            try
            {
                IEnumerable<EmployeeSyncStatus> syncStatuses;

                if (!string.IsNullOrEmpty(status))
                {
                    // Validate status value
                    var validStatuses = new[] { SyncStatusConstants.Synced, SyncStatusConstants.Failed,
                                              SyncStatusConstants.Pending, SyncStatusConstants.NeverSynced };

                    if (!validStatuses.Contains(status))
                    {
                        return BadRequest($"Invalid status. Valid values are: {string.Join(", ", validStatuses)}");
                    }

                    syncStatuses = await _syncService.GetSyncStatusesByStatusAsync(status);
                }
                else
                {
                    syncStatuses = await _syncService.GetAllSyncStatusesAsync();
                }

                var syncStatusList = syncStatuses.ToList();

                var response = new BulkSyncStatusResponseDto
                {
                    TotalCount = syncStatusList.Count,
                    SyncedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Synced),
                    FailedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Failed),
                    PendingCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Pending),
                    NeverSyncedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.NeverSynced),
                    SyncStatuses = syncStatusList.ToDtoList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get sync statistics summary
        /// </summary>
        /// <returns>Sync statistics</returns>
        [HttpGet("statistics")]
        public async Task<ActionResult<SyncStatisticsDto>> GetSyncStatistics()
        {
            try
            {
                var allSyncStatuses = await _syncService.GetAllSyncStatusesAsync();
                var allPersonnel = await _lynksDbService.GetAllPersonnelAsync();

                var syncStatusList = allSyncStatuses.ToList();
                var personnelList = allPersonnel.ToList();

                var syncedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Synced);
                var totalWithStatus = syncStatusList.Count;
                var successRate = totalWithStatus > 0 ? (decimal)syncedCount / totalWithStatus * 100 : 0;

                var statistics = new SyncStatisticsDto
                {
                    TotalPersonnel = personnelList.Count,
                    TotalWithSyncStatus = totalWithStatus,
                    SyncedCount = syncedCount,
                    FailedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Failed),
                    PendingCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.Pending),
                    NeverSyncedCount = syncStatusList.Count(s => s.sync_status == SyncStatusConstants.NeverSynced),
                    SyncSuccessRate = Math.Round(successRate, 2),
                    LastUpdated = DateTime.UtcNow
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new sync status entry
        /// </summary>
        /// <param name="createDto">Sync status creation data</param>
        /// <returns>Created sync status</returns>
        [HttpPost]
        public async Task<ActionResult<EmployeeSyncStatusDto>> CreateSyncStatus([FromBody] CreateEmployeeSyncStatusDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if personnel exists
                var personnel = await _lynksDbService.GetPersonnelByIdAsync(createDto.PersonnelId);
                if (personnel == null)
                {
                    return BadRequest($"Personnel with ID {createDto.PersonnelId} does not exist");
                }

                var syncStatus = createDto.ToEntity();
                var createdSyncStatus = await _syncService.CreateSyncStatusAsync(syncStatus);

                return CreatedAtAction(
                    nameof(GetSyncStatus),
                    new { personnelId = createdSyncStatus.personnel_id },
                    createdSyncStatus.ToDto()
                );
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark sync as successful for a personnel
        /// </summary>
        /// <param name="markSuccessfulDto">Success marking data</param>
        /// <returns>Updated sync status</returns>
        [HttpPost("mark-successful")]
        public async Task<ActionResult<EmployeeSyncStatusDto>> MarkSyncSuccessful([FromBody] MarkSyncSuccessfulDto markSuccessfulDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var updatedSyncStatus = await _syncService.MarkSyncSuccessfulAsync(
                    markSuccessfulDto.PersonnelId,
                    markSuccessfulDto.TelpLastModified,
                    markSuccessfulDto.LynksLastModified
                );

                return Ok(updatedSyncStatus.ToDto());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark sync as failed for a personnel
        /// </summary>
        /// <param name="markFailedDto">Failure marking data</param>
        /// <returns>Updated sync status</returns>
        [HttpPost("mark-failed")]
        public async Task<ActionResult<EmployeeSyncStatusDto>> MarkSyncFailed([FromBody] MarkSyncFailedDto markFailedDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var updatedSyncStatus = await _syncService.MarkSyncFailedAsync(
                    markFailedDto.PersonnelId,
                    markFailedDto.ErrorMessage,
                    markFailedDto.FailedFields
                );

                return Ok(updatedSyncStatus.ToDto());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark sync as pending for a personnel
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>Updated sync status</returns>
        [HttpPost("{personnelId}/mark-pending")]
        public async Task<ActionResult<EmployeeSyncStatusDto>> MarkSyncPending(int personnelId)
        {
            try
            {
                var updatedSyncStatus = await _syncService.MarkSyncPendingAsync(personnelId);
                return Ok(updatedSyncStatus.ToDto());
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get failed sync statuses
        /// </summary>
        /// <returns>List of failed sync statuses</returns>
        [HttpGet("failed")]
        public async Task<ActionResult<List<EmployeeSyncStatusDto>>> GetFailedSyncs()
        {
            try
            {
                var failedSyncs = await _syncService.GetFailedSyncsAsync();
                return Ok(failedSyncs.ToDtoList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get pending sync statuses
        /// </summary>
        /// <returns>List of pending sync statuses</returns>
        [HttpGet("pending")]
        public async Task<ActionResult<List<EmployeeSyncStatusDto>>> GetPendingSyncs()
        {
            try
            {
                var pendingSyncs = await _syncService.GetPendingSyncsAsync();
                return Ok(pendingSyncs.ToDtoList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get never synced statuses
        /// </summary>
        /// <returns>List of never synced statuses</returns>
        [HttpGet("never-synced")]
        public async Task<ActionResult<List<EmployeeSyncStatusDto>>> GetNeverSynced()
        {
            try
            {
                var neverSynced = await _syncService.GetNeverSyncedAsync();
                return Ok(neverSynced.ToDtoList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete sync status for a personnel
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{personnelId}")]
        public async Task<ActionResult> DeleteSyncStatus(int personnelId)
        {
            try
            {
                var deleted = await _syncService.DeleteSyncStatusAsync(personnelId);

                if (!deleted)
                {
                    return NotFound($"Sync status for personnel ID {personnelId} not found");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get failed fields for a specific personnel
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>List of failed fields</returns>
        [HttpGet("{personnelId}/failed-fields")]
        public async Task<ActionResult<List<string>>> GetFailedFields(int personnelId)
        {
            try
            {
                var failedFields = await _syncService.GetFailedFieldsAsync(personnelId);

                if (failedFields == null)
                {
                    return NotFound($"No failed fields found for personnel ID {personnelId}");
                }

                return Ok(failedFields);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get sync attempts count for a specific personnel
        /// </summary>
        /// <param name="personnelId">Personnel ID</param>
        /// <returns>Number of sync attempts</returns>
        [HttpGet("{personnelId}/attempts")]
        public async Task<ActionResult<int>> GetSyncAttempts(int personnelId)
        {
            try
            {
                var attempts = await _syncService.GetTotalSyncAttemptsAsync(personnelId);
                return Ok(attempts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}