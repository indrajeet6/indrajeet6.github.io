using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FamilyRecipeAPI.Models;
using System.Data.SqlClient;

namespace FamilyRecipeAPI.Functions
{
    public static class GetRecipes
    {
        [FunctionName("GetRecipes")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "recipes")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("GetRecipes function triggered");

            var search = req.Query["search"].ToString();
            var category = req.Query["category"].ToString();

            try
            {
                var recipes = new List<Recipe>();
                var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");

                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var query = @"
                        SELECT r.RecipeID, r.Name, r.Category, r.Ingredients, 
                               r.Instructions, r.PhotoURL, r.CreatedAt,
                               u.Username as Author
                        FROM Recipes r
                        JOIN Users u ON r.AuthorID = u.UserID
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(search))
                    {
                        query += " AND (r.Name LIKE @Search OR r.Ingredients LIKE @Search)";
                    }

                    if (!string.IsNullOrEmpty(category))
                    {
                        query += " AND r.Category = @Category";
                    }

                    query += " ORDER BY r.CreatedAt DESC";

                    using (var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(search))
                        {
                            command.Parameters.AddWithValue("@Search", $"%{search}%");
                        }

                        if (!string.IsNullOrEmpty(category))
                        {
                            command.Parameters.AddWithValue("@Category", category);
                        }

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                recipes.Add(new Recipe
                                {
                                    RecipeID = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Category = reader.GetString(2),
                                    Ingredients = reader.GetString(3),
                                    Instructions = reader.GetString(4),
                                    PhotoURL = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    CreatedAt = reader.GetDateTime(6),
                                    Author = reader.GetString(7)
                                });
                            }
                        }
                    }
                }

                return new OkObjectResult(recipes);
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}