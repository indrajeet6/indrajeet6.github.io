using System;
using Microsoft.Data.SqlClient;
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
using System.Data.SqlClient;

namespace FamilyRecipeAPI.Functions
{
    public static class Signup
    {
        [FunctionName("Signup")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signup")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Signup function triggered");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<SignupRequest>(requestBody);

                // Validate input
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return new BadRequestObjectResult(new { error = "Username and password are required" });
                }

                if (request.Username.Length < 3)
                {
                    return new BadRequestObjectResult(new { error = "Username must be at least 3 characters long" });
                }

                if (request.Password.Length < 6)
                {
                    return new BadRequestObjectResult(new { error = "Password must be at least 6 characters long" });
                }

                // Validate email format if provided
                if (!string.IsNullOrWhiteSpace(request.Email))
                {
                    try
                    {
                        var addr = new System.Net.Mail.MailAddress(request.Email);
                        if (addr.Address != request.Email)
                        {
                            return new BadRequestObjectResult(new { error = "Invalid email format" });
                        }
                    }
                    catch
                    {
                        return new BadRequestObjectResult(new { error = "Invalid email format" });
                    }
                }

                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Check if username already exists
                    var checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username";
                    using (var checkCommand = new Microsoft.Data.SqlClient.SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Username", request.Username);
                        var count = (int)await checkCommand.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            return new BadRequestObjectResult(new { error = "Username already exists" });
                        }
                    }

                    // Check if email already exists (if provided)
                    if (!string.IsNullOrWhiteSpace(request.Email))
                    {
                        var emailCheckQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                        using (var emailCheckCommand = new Microsoft.Data.SqlClient.SqlCommand(emailCheckQuery, connection))
                        {
                            emailCheckCommand.Parameters.AddWithValue("@Email", request.Email);
                            var emailCount = (int)await emailCheckCommand.ExecuteScalarAsync();

                            if (emailCount > 0)
                            {
                                return new BadRequestObjectResult(new { error = "Email already registered" });
                            }
                        }
                    }

                    // Hash the password
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                    // Insert new user
                    var insertQuery = @"
                        INSERT INTO Users (Username, PasswordHash, Email, CreatedAt)
                        VALUES (@Username, @PasswordHash, @Email, GETDATE())";

                    using (var insertCommand = new Microsoft.Data.SqlClient.SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@Username", request.Username);
                        insertCommand.Parameters.AddWithValue("@PasswordHash", passwordHash);
                        insertCommand.Parameters.AddWithValue("@Email",
                            string.IsNullOrWhiteSpace(request.Email) ? (object)DBNull.Value : request.Email);

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }

                log.LogInformation($"New user registered: {request.Username}");

                return new OkObjectResult(new SignupResponse
                {
                    Message = "Account created successfully! You can now login.",
                    Username = request.Username
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Error during signup: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}