using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly LegacyAuthenticationService _authService;
        private readonly IConfiguration _configuration;
        private readonly ILynksDbService _dbService;
        private readonly ChiefsManager _chiefsManager;
        private readonly JWTTokenValidator _jwtTokenValidator;
        private readonly ILogger<AuthenticationController> _logger;

        public AuthenticationController(
            LegacyAuthenticationService authService,
            IConfiguration configuration,
            ILynksDbService dbService,
            ChiefsManager chiefsManager,
            JWTTokenValidator jwtTokenValidator,
            ILogger<AuthenticationController> logger)
        {
            _authService = authService;
            _configuration = configuration;
            _dbService = dbService;
            _chiefsManager = chiefsManager;
            _jwtTokenValidator = jwtTokenValidator;
            _logger = logger;
        }

        /// <summary>
        /// Validates a JWT token
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>OK if token is valid, Unauthorized otherwise</returns>
        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Token validation failed: Token is null or empty");
                return Unauthorized("Валидация не прошла. Токен пуст или отсутствует.");
            }

            var principal = _jwtTokenValidator.ValidateToken(token);

            if (principal == null)
            {
                _logger.LogWarning("Token validation failed: Invalid token");
                return Unauthorized("Валидация не прошла, неправильный токен.");
            }

            _logger.LogInformation("Token validation succeeded");
            return Ok();
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token
        /// </summary>
        /// <param name="model">The authentication model containing username and password</param>
        /// <returns>JWT token if authentication is successful</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserForAuthentication model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.username) || string.IsNullOrEmpty(model.password))
                {
                    return BadRequest("Имя пользователя и пароль обязательны.");
                }

                if (model.time_for_being_authenticated <= 0)
                {
                    return BadRequest("Время, выбранное для аутентификации недопустимо.");
                }

                // Authenticate user
                var (token, user) = await _authService.AuthenticateAsync(model.username, model.password);

                if (user == null)
                {
                    _logger.LogWarning($"Login failed: User {model.username} not found or invalid credentials");
                    return Unauthorized("Аутентификация не успешна. Неверное имя пользователя или пароль.");
                }

                if (token == null)
                {
                    return Unauthorized("Аутентификация не успешна. Вход не выполнен.");
                }

                // If the user is a chief, check if another chief is already online
                if (user.Role?.role_type == "ChiefOfDepartment" &&
                    _chiefsManager.GetChiefInfo(user.department_id) != null &&
                    _chiefsManager.GetChiefInfo(user.department_id).ChiefId != user.id.ToString())
                {
                    return CustomForbid("Начальник для текущего отдела уже авторизован. Попросите его закрыть приложение и авторизуйтесь спустя 1 минуту.");
                }

                _logger.LogInformation($"User {model.username} successfully logged in");
                return Ok(new { Token = token, Message = "Успешный вход." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for user {model.username}");
                return StatusCode(500, "Произошла ошибка при аутентификации. Пожалуйста, повторите попытку позже.");
            }
        }

        /// <summary>
        /// Changes user credentials (username, password, email)
        /// </summary>
        /// <param name="credentials">The new credentials</param>
        /// <returns>OK if credentials were updated successfully</returns>
        [HttpPatch("change-credentials")]
        [Authorize]
        public async Task<IActionResult> ChangeCredentials([FromBody] UserCredentials credentials)
        {
            try
            {
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrWhiteSpace(username))
                {
                    return BadRequest("Имя пользователя не определено.");
                }

                // Validate credentials
                if (!ValidateCredentials(credentials, username))
                {
                    return BadRequest("Валидация данных не пройдена. Перепроверьте указанные данные.");
                }

                // Get current user
                var user = await _dbService.GetUserByUsernameAsync(username);
                if (user == null)
                {
                    return NotFound("Пользователь не найден.");
                }

                // Check if new username already exists
                if (credentials.Login != username)
                {
                    var existingUser = await _dbService.GetUserByUsernameAsync(credentials.Login);
                    if (existingUser != null)
                    {
                        return BadRequest("Пользователь с таким именем уже существует.");
                    }
                }

                // Update user properties
                user.username = credentials.Login;
                user.password_hash = BCrypt.Net.BCrypt.HashPassword(credentials.Password);
                user.current_email = credentials.Email;

                // Save user
                await _dbService.UpdateUserAsync(user);

                _logger.LogInformation($"User {username} successfully changed credentials");
                return Ok("Учетные данные успешно обновлены.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user credentials");
                return StatusCode(500, "Не смогли обновить данные пользователя. Что-то пошло не так.");
            }
        }

        /// <summary>
        /// Test endpoint for authorization
        /// </summary>
        /// <returns>Message indicating successful authorization</returns>
        [Authorize]
        [HttpGet("secure-data")]
        public IActionResult GetSecureData()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var departmentId = User.FindFirst("DepartmentId")?.Value;

            _logger.LogInformation($"Secure data accessed by {username}, Role: {role}, Department: {departmentId}");
            return Ok("Эта информация доступна только для авторизованных пользователей.");
        }

        /// <summary>
        /// Creates a custom Forbidden response with a message
        /// </summary>
        private IActionResult CustomForbid(string message)
        {
            var result = new ObjectResult(new { Message = message })
            {
                StatusCode = (int)HttpStatusCode.Forbidden
            };
            return result;
        }

        /// <summary>
        /// Validates user credentials
        /// </summary>
        private bool ValidateCredentials(UserCredentials credentials, string currentUsername)
        {
            // Check if login is valid
            if (string.IsNullOrWhiteSpace(credentials.Login) || credentials.Login.Length < 3)
            {
                return false;
            }

            // Check if password is valid
            if (string.IsNullOrWhiteSpace(credentials.Password) || credentials.Password.Length < 6)
            {
                return false;
            }

            // Check if email is valid (if provided)
            if (!string.IsNullOrWhiteSpace(credentials.Email))
            {
                var emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                if (!Regex.IsMatch(credentials.Email, emailPattern))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Model for authentication requests
    /// </summary>
    public class UserForAuthentication
    {
        public string username { get; set; }
        public string password { get; set; }
        public int time_for_being_authenticated { get; set; } = 480; // Default 8 hours
    }

    /// <summary>
    /// Model for credential change requests
    /// </summary>
    public class UserCredentials
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
    }
}