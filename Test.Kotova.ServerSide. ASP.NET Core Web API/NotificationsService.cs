using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    /// <summary>
    /// Service for managing notifications
    /// </summary>
    public class NotificationsService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILynksDbService _dbService;
        private readonly ILogger<NotificationsService> _logger;

        public NotificationsService(
            IHubContext<NotificationHub> hubContext,
            ILynksDbService dbService,
            ILogger<NotificationsService> logger)
        {
            _hubContext = hubContext;
            _dbService = dbService;
            _logger = logger;
        }

        /// <summary>
        /// Sends a notification to a specific department
        /// </summary>
        /// <param name="departmentId">The ID of the department</param>
        /// <param name="message">The notification message</param>
        /// <param name="senderId">The ID of the sender</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SendDepartmentNotificationAsync(int departmentId, string message, int senderId)
        {
            try
            {
                // Send notification through SignalR
                await _hubContext.Clients.Group($"Department-{departmentId}")
                    .SendAsync("ReceiveNotification", new
                    {
                        Type = "Department",
                        Message = message,
                        Timestamp = DateTime.Now,
                        DepartmentId = departmentId,
                        SenderId = senderId
                    });

                _logger.LogInformation($"Notification sent to Department-{departmentId}: {message}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to Department-{departmentId}");
                return false;
            }
        }

        /// <summary>
        /// Sends a notification to all users when a new instruction is assigned
        /// </summary>
        /// <param name="instruction">The instruction</param>
        /// <returns>True if successful</returns>
        public async Task<bool> NotifyNewInstructionAsync(Instruction instruction)
        {
            try
            {
                var departmentId = instruction.department_id;
                var message = $"New instruction: {instruction.cause_of_instruction}";

                // Send notification through SignalR
                await _hubContext.Clients.Group($"Department-{departmentId}")
                    .SendAsync("ReceiveNotification", new
                    {
                        Type = "Instruction",
                        Message = message,
                        Timestamp = DateTime.Now,
                        InstructionId = instruction.instruction_id,
                        DepartmentId = departmentId
                    });

                _logger.LogInformation($"New instruction notification sent to Department-{departmentId}: {message}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending new instruction notification for instruction {instruction.instruction_id}");
                return false;
            }
        }

        /// <summary>
        /// Notifies users when an instruction is marked as passed by everyone
        /// </summary>
        /// <param name="instructionId">The ID of the instruction</param>
        /// <returns>True if successful</returns>
        public async Task<bool> NotifyInstructionCompletedAsync(int instructionId)
        {
            try
            {
                var instruction = await _dbService.GetInstructionByIdAsync(instructionId);
                if (instruction == null)
                {
                    _logger.LogWarning($"Instruction {instructionId} not found");
                    return false;
                }

                var departmentId = instruction.department_id;
                var message = $"Instruction '{instruction.cause_of_instruction}' has been completed by all employees";

                // Send notification through SignalR
                await _hubContext.Clients.Group($"Department-{departmentId}")
                    .SendAsync("ReceiveNotification", new
                    {
                        Type = "InstructionCompleted",
                        Message = message,
                        Timestamp = DateTime.Now,
                        InstructionId = instructionId,
                        DepartmentId = departmentId
                    });

                _logger.LogInformation($"Instruction completed notification sent for instruction {instructionId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending instruction completed notification for instruction {instructionId}");
                return false;
            }
        }

        /// <summary>
        /// Sends a notification to a specific user
        /// </summary>
        /// <param name="userId">The ID of the user</param>
        /// <param name="message">The notification message</param>
        /// <param name="senderId">The ID of the sender</param>
        /// <returns>True if successful</returns>
        public async Task<bool> SendUserNotificationAsync(int userId, string message, int senderId)
        {
            try
            {
                // Send notification through SignalR to all connections of this user
                await _hubContext.Clients.User(userId.ToString())
                    .SendAsync("ReceiveNotification", new
                    {
                        Type = "Personal",
                        Message = message,
                        Timestamp = DateTime.Now,
                        SenderId = senderId
                    });

                _logger.LogInformation($"Notification sent to user {userId}: {message}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending notification to user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Broadcasts a notification to all connected users
        /// </summary>
        /// <param name="message">The notification message</param>
        /// <param name="senderId">The ID of the sender</param>
        /// <returns>True if successful</returns>
        public async Task<bool> BroadcastNotificationAsync(string message, int senderId)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("ReceiveNotification", new
                    {
                        Type = "Broadcast",
                        Message = message,
                        Timestamp = DateTime.Now,
                        SenderId = senderId
                    });

                _logger.LogInformation($"Broadcast notification sent: {message}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting notification");
                return false;
            }
        }
    }
}
