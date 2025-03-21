using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using BC = BCrypt.Net.BCrypt;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API
{
    /// <summary>
    /// Service for legacy authentication operations
    /// </summary>
    public class LegacyAuthenticationService
    {
        private readonly ILynksDbService _dbService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LegacyAuthenticationService> _logger;
        private readonly ChiefsManager _chiefsManager;

        public LegacyAuthenticationService(
            ILynksDbService dbService,
            IConfiguration configuration,
            ILogger<LegacyAuthenticationService> logger,
            ChiefsManager chiefsManager)
        {
            _dbService = dbService;
            _configuration = configuration;
            _logger = logger;
            _chiefsManager = chiefsManager;
        }

        /// <summary>
        /// Authenticates a user and generates a JWT token
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        /// <returns>Token and user if successful, null otherwise</returns>
        public async Task<(string Token, User User)> AuthenticateAsync(string username, string password)
        {
            try
            {
                var user = await _dbService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    _logger.LogWarning($"Authentication failed: User {username} not found");
                    return (null, null);
                }

                if (!BC.Verify(password, user.password_hash))
                {
                    _logger.LogWarning($"Authentication failed: Invalid password for user {username}");
                    return (null, null);
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                // If the user is a chief, update their online status
                if (user.Role?.role_type == "ChiefOfDepartment")
                {
                    _chiefsManager.TrySignInChief(user.department_id, user.id.ToString(), null);
                    await UpdateDepartmentChiefStatus(user.department_id, true);
                }

                _logger.LogInformation($"User {username} authenticated successfully");
                return (token, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during authentication for user {username}");
                return (null, null);
            }
        }

        /// <summary>
        /// Perform login with a user object and plain password
        /// </summary>
        /// <param name="user">The user object</param>
        /// <param name="plainPassword">The plain text password</param>
        /// <returns>Authentication result and user object</returns>
        public (bool IsAuthenticated, User User) PerformLogin(User user, string plainPassword)
        {
            if (user == null)
            {
                _logger.LogWarning("Authentication failed: User is null");
                return (false, null);
            }

            bool isPasswordValid = BC.Verify(plainPassword, user.password_hash);

            if (!isPasswordValid)
            {
                _logger.LogWarning($"Authentication failed: Invalid password for user {user.username}");
                return (false, null);
            }

            _logger.LogInformation($"User {user.username} authenticated successfully");
            return (true, user);
        }

        /// <summary>
        /// Simple authentication for users
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        /// <returns>Authentication result and user object</returns>
        public async Task<(bool IsAuthenticated, User User)> SimpleAuthenticationUserAsync(string username, string password)
        {
            try
            {
                var user = await _dbService.GetUserByUsernameAsync(username);

                if (user == null)
                {
                    _logger.LogWarning($"Authentication failed: User {username} not found");
                    return (false, null);
                }

                if (!BC.Verify(password, user.password_hash))
                {
                    _logger.LogWarning($"Authentication failed: Invalid password for user {username}");
                    return (false, null);
                }

                _logger.LogInformation($"User {username} authenticated successfully");
                return (true, user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during simple authentication for user {username}");
                return (false, null);
            }
        }

        /// <summary>
        /// Creates a new user account
        /// </summary>
        /// <param name="username">The username</param>
        /// <param name="password">The password</param>
        /// <param name="roleId">The role ID</param>
        /// <param name="departmentId">The department ID</param>
        /// <param name="personnelId">The personnel ID</param>
        /// <param name="email">The email address (optional)</param>
        /// <returns>The created user if successful, null otherwise</returns>
        public async Task<User> CreateUserAsync(string username, string password, int roleId, int departmentId, int personnelId, string email = null)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _dbService.GetUserByUsernameAsync(username);
                if (existingUser != null)
                {
                    _logger.LogWarning($"User creation failed: Username {username} already exists");
                    return null;
                }

                // Hash the password
                var passwordHash = BC.HashPassword(password, workFactor: 12);

                // Create new user object
                var user = new User
                {
                    username = username,
                    password_hash = passwordHash,
                    user_role_id = roleId,
                    department_id = departmentId,
                    personnel_id = personnelId,
                    current_email = email
                };

                // Save user to database
                var createdUser = await _dbService.CreateUserAsync(user);
                _logger.LogInformation($"User {username} created successfully");
                return createdUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user {username}");
                return null;
            }
        }

        /// <summary>
        /// Updates the department chief online status
        /// </summary>
        /// <param name="departmentId">The department ID</param>
        /// <param name="isOnline">The online status</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> UpdateDepartmentChiefStatus(int departmentId, bool isOnline)
        {
            try
            {
                var department = await _dbService.GetDepartmentByIdAsync(departmentId);
                if (department == null)
                {
                    _logger.LogWarning($"Department {departmentId} not found");
                    return false;
                }

                department.is_chief_online = isOnline;
                department.last_online_set_UTC = DateTime.UtcNow;
                await _dbService.UpdateDepartmentAsync(department);

                _logger.LogInformation($"Department {departmentId} chief status updated to {(isOnline ? "online" : "offline")}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating department {departmentId} chief status");
                return false;
            }
        }

        /// <summary>
        /// Generates a JWT token for the user
        /// </summary>
        /// <param name="user">The user</param>
        /// <returns>JWT token</returns>
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtConfig:Secret"]);

            var claims = new List<Claim>
            {
                new Claim("nameid", user.id.ToString()),
                new Claim("unique_name", user.username),
                new Claim("role", user.Role?.role_type ?? "User"),
                new Claim("DepartmentId", user.department_id.ToString())
            };

            if (user.Personnel != null)
            {
                claims.Add(new Claim("PersonnelId", user.personnel_id.ToString()));
                claims.Add(new Claim("PersonnelNumber", user.Personnel.personnel_number));

                // Get employee information for full name only
                var employee = _dbService.GetEmployeeFullNameAsync(user.personnel_id, user.department_id).Result;
                if (!string.IsNullOrEmpty(employee))
                {
                    claims.Add(new Claim("FullName", employee));
                }
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8), // Token valid for 8 hours
                Issuer = _configuration["JwtConfig:Issuer"],
                Audience = _configuration["JwtConfig:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}