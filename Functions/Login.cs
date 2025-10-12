using System;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using BCrypt.Net;
using FamilyRecipeAPI.Models;
using FamilyRecipeAPI.Services;

namespace FamilyRecipeAPI.Functions
{
    public static class Login
    {
        [FunctionName("Login")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Login function triggered");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<LoginRequest>(requestBody);

                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT UserID, Username, PasswordHash 
                        FROM Users 
                        WHERE Username = @Username";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Username", request.Username);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return new ObjectResult(new { error = "Invalid credentials" })
                                {
                                    StatusCode = 401
                                };
                            }

                            var userId = reader.GetInt32(0);
                            var username = reader.GetString(1);
                            var passwordHash = reader.GetString(2);

                            // Verify password
                            if (!BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
                            {
                                return new ObjectResult(new { error = "Invalid credentials" })
                                {
                                    StatusCode = 401
                                };
                            }

                            // Generate JWT token
                            var jwtSecret = Environment.GetEnvironmentVariable("JWTSecret");
                            var tokenService = new TokenService(jwtSecret);
                            var token = tokenService.GenerateToken(userId, username);

                            return new OkObjectResult(new LoginResponse
                            {
                                Token = token,
                                Username = username
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}