using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2016.Drawing.Command;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Path = System.IO.Path;
using Newtonsoft.Json;
using Kotova.CommonClasses;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Claims;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Models;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.Configuration;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Services;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using System.Data.Common;
using Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Tracing;
using System.Text.Json;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Net;
using Microsoft.Data.SqlClient;
using DocumentFormat.OpenXml.Bibliography;
using System.Data;
using System.Timers;
using System.Transactions;
using System.Data.SqlClient;
using Department = Kotova.CommonClasses.Department;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Globalization;

namespace Test.Kotova.ServerSide._ASP.NET_Core_Web_API.Controllers
{
    public class AuthenticationController : ControllerBase
    {
        private readonly LegacyAuthenticationService _legacyAuthService;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContextUsers _context;
        private readonly ChiefsManager _chiefsManager;
        private readonly JwtTokenValidator _jwtTokenValidator;
        public AuthenticationController(LegacyAuthenticationService legacyAuthService, IConfiguration configuration, ApplicationDbContextUsers context, ChiefsManager chiefsManager, JwtTokenValidator jwtTokenValidator)
        {
            _legacyAuthService = legacyAuthService;
            _configuration = configuration;
            _context = context;
            _chiefsManager = chiefsManager;
            _jwtTokenValidator = jwtTokenValidator;
        }

        private async Task<int> GetDepartmentIdFromUserName(string username)
        {
            var user = await _context.Users
            .Where(u => u.username == username)
            .Select(u => new { u.department_id })
            .FirstOrDefaultAsync();
            if (user == null)
            {
                return -1;
            }
            return user.department_id;
        }

        [HttpPost("validate-token")] //ПРОВЕРЕНО
        public IActionResult ValidateToken([FromBody] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Token is null or empty.");
                return Unauthorized("Валидация не прошла. Токен пуст или отсутсвует.");
            }

            var principal = _jwtTokenValidator.ValidateToken(token);

            if (principal == null)
            {
                Console.WriteLine("Token validation failed.");
                return Unauthorized("Валидация не прошла, неправильный токен.");
            }

            Console.WriteLine("Token validation succeeded.");
            return Ok();
        }

        [HttpPost("login")] //ПРОВЕРЕНО
        public async Task<IActionResult> Login([FromBody] UserForAuthentication model)
        {
            var userTemp = await _context.Users.FirstOrDefaultAsync(u => u.username == model.username);

            
            if (userTemp == null)
            {
                return BadRequest($"Пользователь с именем '{model.username}' не был найден");
            }
            if (model.time_for_being_authenticated <= 0)
            {
                return BadRequest("Время, выбранное для аутентификации недопустимо.");
            }

            var userRole = userTemp.user_role;

            (bool?, User?) authenticationModel = _legacyAuthService.PerformLogin(userTemp, model.password);
            if (authenticationModel.Item1 == true)
            {
                if (userRole == 2 && _chiefsManager.IsChiefOnline(await GetDepartmentIdFromUserName(model.username)))
                {
                    return CustomForbid("Начальник для текущего отдела уже авторизован. Попросите его закрыть приложение и авторизуйтесь спустя 1 минуту.");
                }
                /*if (await _chiefsManager.IsChiefOnlineAsync(authenticationModel.Item2.department_id))
                {
                    return CustomForbid("Начальник для текущего отдела уже авторизован. Попросите его закрыть приложение и авторизуйтесь спустя 1 минуту.");
                }*/ //Теперь проверка через NotificationHub!

                try
                {
                    var user = authenticationModel.Item2;
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.username),
                        new Claim(ClaimTypes.Role, RoleModelIntToString(user.user_role)),
                        new Claim("department_id", (await GetDepartmentIdFromUserName(model.username)).ToString()),
                    };

                    string secret = _configuration["JwtConfig:Secret"];
                    var token = GenerateJwtToken(claims, secret, model.time_for_being_authenticated);

                    return Ok(new { Token = token, Message = "Успешный вход." });
                }
                catch (ArgumentException ex)
                {
                    return BadRequest(ex.Message);
                }
            }
            else if (authenticationModel.Item1 == null)
            {
                return Unauthorized("У пользователя нет персонального номера. Подождите пока он появится.");
            }
            else
            {
                return Unauthorized("Аутентификация не успешна. Вход не выполнен.");
            }
        }

        public IActionResult CustomForbid(string message)
        {
            var result = new ObjectResult(new { Message = message })
            {
                StatusCode = (int)HttpStatusCode.Forbidden
            };
            return result;
        }

        [HttpPatch]
        [Route("change_credentials")] //ПРОВЕРЕНО
        [Authorize]
        public async Task<IActionResult> ChangeCredentials([FromBody] UserCredentials credentials)
        {
            var authorizationHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            string jwtToken = authorizationHeader?.StartsWith("Bearer ") == true ? authorizationHeader.Substring("Bearer ".Length).Trim() : null;
            if (string.IsNullOrEmpty(jwtToken))
            {
                return BadRequest("JWT token is null or empty");
            }
            string user = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrWhiteSpace(user))
            {
                return BadRequest("User is null or empty");
            }

            CredentialValidation credentialValidation = new CredentialValidation();
            if (credentialValidation.CheckForValidation(credentials, user))
            {
                try
                {
                    return await UpdateCredentialsForUserInDB(credentials, user);
                }
                catch
                {
                    return BadRequest("Не смогли обновить новые данные для пользователя. Что-то пошло не так.");
                }
            }
            else
            {
                return BadRequest("Валидация данных не пройдена. Перепроверьте указанные данные");
            }
        }

        private async Task<IActionResult> UpdateCredentialsForUserInDB(UserCredentials credentials, string user)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var newUserExistInDB = await _context.Users.FirstOrDefaultAsync(u => u.username == credentials.Login);
                    if (newUserExistInDB != null && credentials.Login != user)
                    {
                        return BadRequest("Пользователь с таким именем уже существует.");
                    }

                    var userToUpdate = await _context.Users.FirstOrDefaultAsync(u => u.username == user);

                    if (userToUpdate != null)
                    {
                        userToUpdate.username = credentials.Login;
                        userToUpdate.password_hash = Encryption_Kotova.HashPassword(credentials.Password);
                        userToUpdate.current_email = credentials.Email;

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return Ok();
                    }
                    else
                    {
                        throw new Exception("Пользователь не найден");
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return StatusCode(500, "Internal server error. Please try again later.");
                }
            }
        }

        [Authorize]
        [HttpGet("securedata")] //ПРОВЕРЕНО
        public IActionResult GetSecureData()
        {
            return Ok("Эта информация доступна только для всех авторизованных пользователей.");
        }

        private string RoleModelIntToString(int user_role)
        {
            return user_role switch
            {
                1 => "User",
                2 => "ChiefOfDepartment",
                3 => "Coordinator",
                4 => "Management",
                5 => "Administrator",
                _ => throw new ArgumentException($"Роль под номером: {user_role} не допустима, что-то пошло не так!")
            };
        }

        private bool CheckForValidPersonnelNumber(string input)
        {
            string pattern = @"^\d{10}$";
            return Regex.IsMatch(input, pattern);
        }

        public string GenerateJwtToken(List<Claim> claims, string secret, int timeForExpiration)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "yourdomain.com",
                audience: "yourdomain.com",
                claims: claims,
                expires: DateTime.Now.AddMinutes(timeForExpiration),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
