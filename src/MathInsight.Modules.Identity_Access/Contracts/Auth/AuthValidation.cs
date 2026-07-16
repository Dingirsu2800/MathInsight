namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

/// <summary>Shared validation constants for auth DTOs.</summary>
public static class AuthValidation
{
    // BR-08: 8–128 chars, at least one uppercase, one lowercase, one digit, one special char.
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,128}$";

    public const string PasswordMessage =
        "Password must be 8-128 characters and include an uppercase letter, a lowercase letter, a number, and a special character.";
}
