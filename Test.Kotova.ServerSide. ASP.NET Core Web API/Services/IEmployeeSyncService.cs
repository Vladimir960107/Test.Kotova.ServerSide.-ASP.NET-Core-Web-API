using Microsoft.EntityFrameworkCore;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using System.Text.Json;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services
{
    /// <summary>
    /// Interface for managing employee synchronization status
    /// </summary>
    /// <summary>
    /// Interface for managing employee synchronization status
    /// </summary>
    public interface IEmployeeSyncService
    {
        // Read operations
        Task<EmployeeSyncStatus?> GetSyncStatusAsync(int personnelId);
        Task<IEnumerable<EmployeeSyncStatus>> GetAllSyncStatusesAsync();
        Task<IEnumerable<EmployeeSyncStatus>> GetSyncStatusesByStatusAsync(string status);
        Task<IEnumerable<EmployeeSyncStatus>> GetFailedSyncsAsync();
        Task<IEnumerable<EmployeeSyncStatus>> GetPendingSyncsAsync();
        Task<IEnumerable<EmployeeSyncStatus>> GetNeverSyncedAsync();

        // Write operations
        Task<EmployeeSyncStatus> CreateSyncStatusAsync(EmployeeSyncStatus syncStatus);
        Task<EmployeeSyncStatus> UpdateSyncStatusAsync(EmployeeSyncStatus syncStatus);
        Task<bool> DeleteSyncStatusAsync(int personnelId);

        // Sync operation helpers
        Task<EmployeeSyncStatus> MarkSyncSuccessfulAsync(int personnelId, DateTime? telpLastModified = null, DateTime? lynksLastModified = null);
        Task<EmployeeSyncStatus> MarkSyncFailedAsync(int personnelId, string errorMessage, List<string>? failedFields = null);
        Task<EmployeeSyncStatus> MarkSyncPendingAsync(int personnelId);
        Task<EmployeeSyncStatus> IncrementSyncAttemptsAsync(int personnelId);

        // Utility methods
        Task<bool> SyncStatusExistsAsync(int personnelId);
        Task<int> GetTotalSyncAttemptsAsync(int personnelId);
        Task<List<string>?> GetFailedFieldsAsync(int personnelId);
    }

    /// <summary>
    /// Service implementation for managing employee synchronization status
    /// </summary>
    public class EmployeeSyncService : IEmployeeSyncService
    {
        private readonly LynksDbContext _context;

        public EmployeeSyncService(LynksDbContext context)
        {
            _context = context;
        }

        #region Read Operations

        public async Task<EmployeeSyncStatus?> GetSyncStatusAsync(int personnelId)
        {
            return await _context.EmployeeSyncStatuses
                .Include(e => e.Personnel)
                .FirstOrDefaultAsync(e => e.personnel_id == personnelId);
        }

        public async Task<IEnumerable<EmployeeSyncStatus>> GetAllSyncStatusesAsync()
        {
            return await _context.EmployeeSyncStatuses
                .Include(e => e.Personnel)
                .ToListAsync();
        }

        public async Task<IEnumerable<EmployeeSyncStatus>> GetSyncStatusesByStatusAsync(string status)
        {
            return await _context.EmployeeSyncStatuses
                .Include(e => e.Personnel)
                .Where(e => e.sync_status == status)
                .ToListAsync();
        }

        public async Task<IEnumerable<EmployeeSyncStatus>> GetFailedSyncsAsync()
        {
            return await GetSyncStatusesByStatusAsync(SyncStatusConstants.Failed);
        }

        public async Task<IEnumerable<EmployeeSyncStatus>> GetPendingSyncsAsync()
        {
            return await GetSyncStatusesByStatusAsync(SyncStatusConstants.Pending);
        }

        public async Task<IEnumerable<EmployeeSyncStatus>> GetNeverSyncedAsync()
        {
            return await GetSyncStatusesByStatusAsync(SyncStatusConstants.NeverSynced);
        }

        #endregion

        #region Write Operations

        public async Task<EmployeeSyncStatus> CreateSyncStatusAsync(EmployeeSyncStatus syncStatus)
        {
            // Ensure personnel exists
            var personnel = await _context.Personnel.FindAsync(syncStatus.personnel_id);
            if (personnel == null)
            {
                throw new ArgumentException($"Personnel with ID {syncStatus.personnel_id} does not exist");
            }

            // Check if sync status already exists
            var existing = await _context.EmployeeSyncStatuses
                .FirstOrDefaultAsync(e => e.personnel_id == syncStatus.personnel_id);

            if (existing != null)
            {
                throw new InvalidOperationException($"Sync status for personnel ID {syncStatus.personnel_id} already exists");
            }

            _context.EmployeeSyncStatuses.Add(syncStatus);
            await _context.SaveChangesAsync();

            return await GetSyncStatusAsync(syncStatus.personnel_id) ?? syncStatus;
        }

        public async Task<EmployeeSyncStatus> UpdateSyncStatusAsync(EmployeeSyncStatus syncStatus)
        {
            var existing = await _context.EmployeeSyncStatuses
                .FirstOrDefaultAsync(e => e.personnel_id == syncStatus.personnel_id);

            if (existing == null)
            {
                throw new ArgumentException($"Sync status for personnel ID {syncStatus.personnel_id} does not exist");
            }

            // Update properties
            existing.last_sync_datetime = syncStatus.last_sync_datetime;
            existing.sync_status = syncStatus.sync_status;
            existing.telp_last_modified = syncStatus.telp_last_modified;
            existing.lynks_last_modified = syncStatus.lynks_last_modified;
            existing.failed_fields = syncStatus.failed_fields;
            existing.sync_attempts = syncStatus.sync_attempts;
            existing.last_error_message = syncStatus.last_error_message;

            await _context.SaveChangesAsync();

            return await GetSyncStatusAsync(syncStatus.personnel_id) ?? existing;
        }

        public async Task<bool> DeleteSyncStatusAsync(int personnelId)
        {
            var syncStatus = await _context.EmployeeSyncStatuses
                .FirstOrDefaultAsync(e => e.personnel_id == personnelId);

            if (syncStatus == null)
            {
                return false;
            }

            _context.EmployeeSyncStatuses.Remove(syncStatus);
            await _context.SaveChangesAsync();
            return true;
        }

        #endregion

        #region Sync Operation Helpers

        public async Task<EmployeeSyncStatus> MarkSyncSuccessfulAsync(int personnelId, DateTime? telpLastModified = null, DateTime? lynksLastModified = null)
        {
            var syncStatus = await GetSyncStatusAsync(personnelId);

            if (syncStatus == null)
            {
                // Create new sync status
                syncStatus = new EmployeeSyncStatus
                {
                    personnel_id = personnelId,
                    sync_status = SyncStatusConstants.Synced,
                    last_sync_datetime = DateTime.UtcNow,
                    telp_last_modified = telpLastModified,
                    lynks_last_modified = lynksLastModified,
                    sync_attempts = 1,
                    failed_fields = null,
                    last_error_message = null
                };

                return await CreateSyncStatusAsync(syncStatus);
            }
            else
            {
                // Update existing
                syncStatus.sync_status = SyncStatusConstants.Synced;
                syncStatus.last_sync_datetime = DateTime.UtcNow;
                syncStatus.telp_last_modified = telpLastModified ?? syncStatus.telp_last_modified;
                syncStatus.lynks_last_modified = lynksLastModified ?? syncStatus.lynks_last_modified;
                syncStatus.sync_attempts++;
                syncStatus.failed_fields = null;
                syncStatus.last_error_message = null;

                return await UpdateSyncStatusAsync(syncStatus);
            }
        }

        public async Task<EmployeeSyncStatus> MarkSyncFailedAsync(int personnelId, string errorMessage, List<string>? failedFields = null)
        {
            var syncStatus = await GetSyncStatusAsync(personnelId);

            var failedFieldsJson = failedFields != null && failedFields.Any()
                ? JsonSerializer.Serialize(failedFields)
                : null;

            if (syncStatus == null)
            {
                // Create new sync status
                syncStatus = new EmployeeSyncStatus
                {
                    personnel_id = personnelId,
                    sync_status = SyncStatusConstants.Failed,
                    last_sync_datetime = null,
                    sync_attempts = 1,
                    failed_fields = failedFieldsJson,
                    last_error_message = errorMessage
                };

                return await CreateSyncStatusAsync(syncStatus);
            }
            else
            {
                // Update existing
                syncStatus.sync_status = SyncStatusConstants.Failed;
                syncStatus.sync_attempts++;
                syncStatus.failed_fields = failedFieldsJson;
                syncStatus.last_error_message = errorMessage;

                return await UpdateSyncStatusAsync(syncStatus);
            }
        }

        public async Task<EmployeeSyncStatus> MarkSyncPendingAsync(int personnelId)
        {
            var syncStatus = await GetSyncStatusAsync(personnelId);

            if (syncStatus == null)
            {
                // Create new sync status
                syncStatus = new EmployeeSyncStatus
                {
                    personnel_id = personnelId,
                    sync_status = SyncStatusConstants.Pending,
                    last_sync_datetime = null,
                    sync_attempts = 0
                };

                return await CreateSyncStatusAsync(syncStatus);
            }
            else
            {
                // Update existing
                syncStatus.sync_status = SyncStatusConstants.Pending;

                return await UpdateSyncStatusAsync(syncStatus);
            }
        }

        public async Task<EmployeeSyncStatus> IncrementSyncAttemptsAsync(int personnelId)
        {
            var syncStatus = await GetSyncStatusAsync(personnelId);

            if (syncStatus == null)
            {
                throw new ArgumentException($"Sync status for personnel ID {personnelId} does not exist");
            }

            syncStatus.sync_attempts++;
            return await UpdateSyncStatusAsync(syncStatus);
        }

        #endregion

        #region Utility Methods

        public async Task<bool> SyncStatusExistsAsync(int personnelId)
        {
            return await _context.EmployeeSyncStatuses
                .AnyAsync(e => e.personnel_id == personnelId);
        }

        public async Task<int> GetTotalSyncAttemptsAsync(int personnelId)
        {
            var syncStatus = await _context.EmployeeSyncStatuses
                .FirstOrDefaultAsync(e => e.personnel_id == personnelId);

            return syncStatus?.sync_attempts ?? 0;
        }

        public async Task<List<string>?> GetFailedFieldsAsync(int personnelId)
        {
            var syncStatus = await _context.EmployeeSyncStatuses
                .FirstOrDefaultAsync(e => e.personnel_id == personnelId);

            if (syncStatus?.failed_fields == null)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(syncStatus.failed_fields);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        #endregion
    }
}