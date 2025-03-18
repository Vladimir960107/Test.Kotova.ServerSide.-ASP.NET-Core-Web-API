using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    /// <summary>
    /// SignalR hub for real-time notifications
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        private readonly ChiefsManager _chiefsManager;
        private readonly static ConcurrentDictionary<string, UserConnection> _connections = new ConcurrentDictionary<string, UserConnection>();

        public NotificationHub(ILogger<NotificationHub> logger, ChiefsManager chiefsManager)
        {
            _logger = logger;
            _chiefsManager = chiefsManager;
        }

        /// <summary>
        /// Triggered when a client connects to the hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            try
            {
                var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userName = Context.User.FindFirst(ClaimTypes.Name)?.Value;
                var departmentIdClaim = Context.User.FindFirst("DepartmentId")?.Value ??
                                       Context.User.FindFirst("department_id")?.Value;
                var role = Context.User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(departmentIdClaim) || !int.TryParse(departmentIdClaim, out int departmentId))
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "System", "Unable to identify user or department.");
                    Context.Abort();
                    return;
                }

                var connection = new UserConnection
                {
                    UserId = userId,
                    UserName = userName,
                    DepartmentId = departmentId,
                    Role = role,
                    ConnectionId = Context.ConnectionId
                };

                // If the user is a chief, handle their sign-in
                if (role == "ChiefOfDepartment")
                {
                    if (!_chiefsManager.TrySignInChief(departmentId, userId, Context.ConnectionId))
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Another department chief is already signed in.");
                        Context.Abort();
                        return;
                    }

                    await Clients.All.SendAsync("ChiefStatusUpdated", departmentId, true);
                }

                _connections.TryAdd(Context.ConnectionId, connection);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Department-{departmentId}");

                _logger.LogInformation($"User {userName} (ID: {userId}) connected to notification hub. Role: {role}, Department: {departmentId}");
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnConnectedAsync for connection {Context.ConnectionId}");
                await base.OnConnectedAsync();
            }
        }

        /// <summary>
        /// Triggered when a client disconnects from the hub
        /// </summary>
        /// <param name="exception">Exception that caused the disconnection, if any</param>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                if (_connections.TryRemove(Context.ConnectionId, out UserConnection connection))
                {
                    // If the user is a chief, update their offline status
                    if (connection.Role == "ChiefOfDepartment")
                    {
                        _chiefsManager.TrySignOutChief(connection.DepartmentId);
                        await Clients.All.SendAsync("ChiefStatusUpdated", connection.DepartmentId, false);
                    }

                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Department-{connection.DepartmentId}");
                    _logger.LogInformation($"User {connection.UserName} (ID: {connection.UserId}) disconnected from notification hub");
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in OnDisconnectedAsync for connection {Context.ConnectionId}");
                await base.OnDisconnectedAsync(exception);
            }
        }

        /// <summary>
        /// Sends a notification to a specific department
        /// </summary>
        /// <param name="departmentId">The ID of the department</param>
        /// <param name="message">The notification message</param>
        [Authorize(Roles = "Administrator,Coordinator,ChiefOfDepartment")]
        public async Task SendDepartmentNotification(int departmentId, string message)
        {
            try
            {
                var userName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var role = Context.User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                await Clients.Group($"Department-{departmentId}").SendAsync("ReceiveNotification", new
                {
                    Type = "Department",
                    Sender = $"{role}: {userName}",
                    Message = message,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation($"Notification sent to Department-{departmentId} by {userName}: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to Department-{departmentId}");
                throw;
            }
        }

        /// <summary>
        /// Sends a notification to all connected clients
        /// </summary>
        /// <param name="message">The notification message</param>
        [Authorize(Roles = "Administrator")]
        public async Task SendMessageToEveryone(string message)
        {
            try
            {
                var userName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "Administrator";

                await Clients.All.SendAsync("ReceiveNotification", new
                {
                    Type = "Broadcast",
                    Sender = "Administrator",
                    Message = message,
                    Timestamp = DateTime.Now
                });

                _logger.LogInformation($"Broadcast notification sent by {userName}: {message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                throw;
            }
        }

        /// <summary>
        /// Sends a notification to a specific user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="message">The notification message</param>
        [Authorize(Roles = "Administrator,Coordinator,ChiefOfDepartment")]
        public async Task SendUserNotification(string userId, string message)
        {
            try
            {
                var senderName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                var role = Context.User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                var userConnections = GetConnectionsByUserId(userId);
                if (userConnections.Count > 0)
                {
                    foreach (var connectionId in userConnections)
                    {
                        await Clients.Client(connectionId).SendAsync("ReceiveNotification", new
                        {
                            Type = "Personal",
                            Sender = $"{role}: {senderName}",
                            Message = message,
                            Timestamp = DateTime.Now
                        });
                    }
                    _logger.LogInformation($"Notification sent to user {userId} by {senderName}: {message}");
                }
                else
                {
                    _logger.LogInformation($"User {userId} is not currently connected");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Updates user's online status manually
        /// </summary>
        [Authorize(Roles = "ChiefOfDepartment")]
        public async Task UpdateChiefStatus()
        {
            try
            {
                var departmentIdClaim = Context.User.FindFirst("DepartmentId")?.Value ??
                                        Context.User.FindFirst("department_id")?.Value;

                if (!string.IsNullOrEmpty(departmentIdClaim) && int.TryParse(departmentIdClaim, out int departmentId))
                {
                    var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    _chiefsManager.TrySignInChief(departmentId, userId, Context.ConnectionId);
                    await Clients.All.SendAsync("ChiefStatusUpdated", departmentId, true);
                    _logger.LogInformation($"Chief of department {departmentId} manually updated their online status");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chief status");
                throw;
            }
        }

        private List<string> GetConnectionsByUserId(string userId)
        {
            var connections = new List<string>();
            foreach (var connection in _connections)
            {
                if (connection.Value.UserId == userId)
                {
                    connections.Add(connection.Key);
                }
            }
            return connections;
        }
    }

    /// <summary>
    /// Represents a user connection to the notification hub
    /// </summary>
    public class UserConnection
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public int DepartmentId { get; set; }
        public string Role { get; set; }
        public string ConnectionId { get; set; }
    }
}
