using System;
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

namespace FamilyRecipeAPI.Functions
{
    public static class UploadPhoto
    {
        [FunctionName("UploadPhoto")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upload-photo")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("UploadPhoto function triggered");

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

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonConvert.DeserializeObject<UploadPhotoRequest>(requestBody);

                if (string.IsNullOrEmpty(request.PhotoBase64))
                {
                    return new BadRequestObjectResult(new { error = "No photo provided" });
                }

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

                return new OkObjectResult(new
                {
                    photoURL = blobClient.Uri.ToString()
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