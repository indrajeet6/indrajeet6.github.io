using System;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using FamilyRecipeAPI.Models;
using FamilyRecipeAPI.Services;
using System.Data.SqlClient;

namespace FamilyRecipeAPI.Functions
{
    public static class AddRecipe
    {
        [FunctionName("AddRecipe")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "recipes")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("AddRecipe function triggered");

            // Verify JWT token
            var authHeader = req.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new UnauthorizedResult();
            }

            var token = authHeader.Substring("Bearer ".Length);
            var jwtSecret = Environment.GetEnvironmentVariable("JWTSecret");
            var tokenService = new TokenService(jwtSecret);
            var principal = tokenService.ValidateToken(token);

            if (principal == null)
            {
                return new UnauthorizedResult();
            }

            var userId = int.Parse(principal.FindFirst("userId").Value);

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<AddRecipeRequest>(requestBody);

                string photoURL = null;

                // Upload photo to Blob Storage if provided
                if (!string.IsNullOrEmpty(request.PhotoBase64))
                {
                    var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnectionString");
                    var blobServiceClient = new BlobServiceClient(blobConnectionString);
                    var containerClient = blobServiceClient.GetBlobContainerClient("recipe-photos");

                    // Generate unique filename
                    var fileName = $"{Guid.NewGuid()}.jpg";
                    var blobClient = containerClient.GetBlobClient(fileName);

                    // Convert base64 to bytes
                    var base64Data = request.PhotoBase64;
                    if (base64Data.Contains(","))
                    {
                        base64Data = base64Data.Split(',')[1];
                    }
                    var imageBytes = Convert.FromBase64String(base64Data);

                    // Upload to blob storage
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        await blobClient.UploadAsync(ms, overwrite: true);
                    }

                    photoURL = blobClient.Uri.ToString();
                }

                // Insert recipe into database
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        INSERT INTO Recipes (Name, Category, Ingredients, Instructions, PhotoURL, AuthorID)
                        VALUES (@Name, @Category, @Ingredients, @Instructions, @PhotoURL, @AuthorID)";

                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", request.Name);
                        command.Parameters.AddWithValue("@Category", request.Category);
                        command.Parameters.AddWithValue("@Ingredients", request.Ingredients);
                        command.Parameters.AddWithValue("@Instructions", request.Instructions);
                        command.Parameters.AddWithValue("@PhotoURL", (object)photoURL ?? DBNull.Value);
                        command.Parameters.AddWithValue("@AuthorID", userId);

                        await command.ExecuteNonQueryAsync();
                    }
                }

                return new OkObjectResult(new
                {
                    message = "Recipe added successfully",
                    photoURL = photoURL
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}