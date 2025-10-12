using System;

namespace FamilyRecipeAPI.Models
{
    public class Recipe
    {
        public int RecipeID { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Ingredients { get; set; }
        public string Instructions { get; set; }
        public string PhotoURL { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddRecipeRequest
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Ingredients { get; set; }
        public string Instructions { get; set; }
        public string PhotoBase64 { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public string Username { get; set; }
    }

    public class UploadPhotoRequest
    {
        public string PhotoBase64 { get; set; }
    }

    public class SignupRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
    }

    public class SignupResponse
    {
        public string Message { get; set; }
        public string Username { get; set; }
    }
}