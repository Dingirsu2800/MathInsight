namespace MathInsight.Modules.Identity_Access.Contracts.Auth;

/// <summary>Shared validation constants for auth DTOs.</summary>
public static class AuthValidation
{
    // BR-08: 8–128 chars, at least one uppercase, one lowercase, one digit, one special char.
    public const string PasswordPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,128}$";

    public const string PasswordMessage =
        "Password must be 8-128 characters and include an uppercase letter, a lowercase letter, a number, and a special character.";

    // Vietnamese phone number as dialled domestically: exactly 10 digits starting with 0
    // (e.g. 0912345678). Well inside Account.PhoneNumber VARCHAR(20).
    public const string PhoneNumberPattern = @"^0\d{9}$";

    public const string PhoneNumberMessage =
        "Phone number must be exactly 10 digits and start with 0.";
}
