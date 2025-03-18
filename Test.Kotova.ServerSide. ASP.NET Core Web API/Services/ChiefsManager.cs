using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    /// <summary>
    /// Manages chiefs of departments and their online status
    /// </summary>
    public class ChiefsManager
    {
        private readonly ILogger<ChiefsManager> _logger;
        private readonly ConcurrentDictionary<int, ChiefInfo> _chiefsByDepartment;
        private Timer _cleanupTimer;

        public ChiefsManager(ILogger<ChiefsManager> logger)
        {
            _logger = logger;
            _chiefsByDepartment = new ConcurrentDictionary<int, ChiefInfo>();

            // Start the cleanup timer to check for chiefs who might have gone offline
            _cleanupTimer = new Timer(CleanupChiefStatus, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Attempts to sign in a chief for a department
        /// </summary>
        /// <param name="departmentId">The department ID</param>
        /// <param name="chiefId">The chief's user ID</param>
        /// <param name="connectionId">The SignalR connection ID</param>
        /// <returns>True if successful, false if another chief is already signed in</returns>
        public bool TrySignInChief(int departmentId, string chiefId, string connectionId)
        {
            try
            {
                // If there's no chief for this department yet, or it's the same chief
                if (!_chiefsByDepartment.TryGetValue(departmentId, out var existingChief) ||
                    (existingChief != null && existingChief.ChiefId == chiefId))
                {
                    var chiefInfo = new ChiefInfo
                    {
                        ChiefId = chiefId,
                        ConnectionId = connectionId,
                        LastActivityTime = DateTime.UtcNow
                    };

                    _chiefsByDepartment[departmentId] = chiefInfo;
                    _logger.LogInformation($"Chief {chiefId} signed in for department {departmentId}");
                    return true;
                }

                _logger.LogWarning($"Chief {chiefId} attempted to sign in for department {departmentId}, but chief {existingChief.ChiefId} is already signed in");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error signing in chief {chiefId} for department {departmentId}");
                return false;
            }
        }

        /// <summary>
        /// Signs out a chief from a department
        /// </summary>
        /// <param name="departmentId">The department ID</param>
        /// <returns>True if successful, false if no chief was signed in</returns>
        public bool TrySignOutChief(int departmentId)
        {
            try
            {
                if (_chiefsByDepartment.TryRemove(departmentId, out var chiefInfo))
                {
                    _logger.LogInformation($"Chief {chiefInfo.ChiefId} signed out from department {departmentId}");
                    return true;
                }

                _logger.LogWarning($"Attempted to sign out chief from department {departmentId}, but no chief was signed in");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error signing out chief from department {departmentId}");
                return false;
            }
        }

        /// <summary>
        /// Gets the chief info for a department
        /// </summary>
        /// <param name="departmentId">The department ID</param>
        /// <returns>Chief info if a chief is signed in, null otherwise</returns>
        public ChiefInfo GetChiefInfo(int departmentId)
        {
            _chiefsByDepartment.TryGetValue(departmentId, out var chiefInfo);
            return chiefInfo;
        }

        /// <summary>
        /// Gets all departments with their chief's online status
        /// </summary>
        /// <returns>A dictionary of department IDs and online status</returns>
        public Dictionary<int, bool> GetAllChiefsStatus()
        {
            return _chiefsByDepartment.ToDictionary(x => x.Key, x => true);
        }

        /// <summary>
        /// Updates a chief's last activity time
        /// </summary>
        /// <param name="departmentId">The department ID</param>
        /// <returns>True if successful, false if no chief is signed in</returns>
        public bool UpdateChiefActivity(int departmentId)
        {
            try
            {
                if (_chiefsByDepartment.TryGetValue(departmentId, out var chiefInfo))
                {
                    chiefInfo.LastActivityTime = DateTime.UtcNow;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating chief activity for department {departmentId}");
                return false;
            }
        }

        /// <summary>
        /// Updates the department in the database with the current chief online status
        /// </summary>
        /// <param name="departmentId">The ID of the department</param>
        /// <param name="dbService">The database service</param>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateDepartmentStatusInDatabaseAsync(int departmentId, ILynksDbService dbService)
        {
            try
            {
                var department = await dbService.GetDepartmentByIdAsync(departmentId);
                if (department == null)
                {
                    _logger.LogWarning($"Department {departmentId} not found");
                    return false;
                }

                var isChiefOnline = _chiefsByDepartment.ContainsKey(departmentId);
                department.is_chief_online = isChiefOnline;
                department.last_online_set_UTC = DateTime.UtcNow;
                await dbService.UpdateDepartmentAsync(department);

                _logger.LogInformation($"Department {departmentId} chief status updated to {(isChiefOnline ? "online" : "offline")} in database");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating department {departmentId} status in database");
                return false;
            }
        }

        private void CleanupChiefStatus(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                var inactivityThreshold = TimeSpan.FromMinutes(30); // Chiefs go offline after 30 minutes of inactivity
                var departmentsToCleanup = new List<int>();

                foreach (var entry in _chiefsByDepartment)
                {
                    var departmentId = entry.Key;
                    var chiefInfo = entry.Value;

                    if ((now - chiefInfo.LastActivityTime) > inactivityThreshold)
                    {
                        departmentsToCleanup.Add(departmentId);
                        _logger.LogInformation($"Chief {chiefInfo.ChiefId} for department {departmentId} has been automatically signed out due to inactivity");
                    }
                }

                foreach (var departmentId in departmentsToCleanup)
                {
                    _chiefsByDepartment.TryRemove(departmentId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chief status cleanup");
            }
        }
    }

    /// <summary>
    /// Information about a department chief
    /// </summary>
    public class ChiefInfo
    {
        public string ChiefId { get; set; }
        public string ConnectionId { get; set; }
        public DateTime LastActivityTime { get; set; }
    }
}