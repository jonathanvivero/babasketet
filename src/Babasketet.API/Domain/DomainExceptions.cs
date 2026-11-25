namespace Babasketet.API.Domain;

public abstract class DomainException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class HouseLimitExceededException(int maxHouses, bool isAnonymous)
    : DomainException(
        "HOUSE_LIMIT_REACHED",
        isAnonymous
            ? $"Los usuarios no registrados pueden gestionar un máximo de {maxHouses} casa. Inicia sesión para ampliar hasta {DomainCatalogs.RegisteredMaxHouses} casas."
            : $"Has alcanzado el límite máximo de {maxHouses} casas permitidas.",
        StatusCodes.Status409Conflict);

public sealed class InvitationExpiredException()
    : DomainException(
        "INVITATION_EXPIRED",
        "El enlace de invitación ha caducado o ha sido revocado.",
        StatusCodes.Status410Gone);

public sealed class ResourceNotFoundException(string resource, object id)
    : DomainException(
        "RESOURCE_NOT_FOUND",
        $"No se encontró {resource} ({id}).",
        StatusCodes.Status404NotFound);

public sealed class ForbiddenAccessException(string message = "No tienes acceso a esta casa.")
    : DomainException("FORBIDDEN", message, StatusCodes.Status403Forbidden);

public sealed class UserBlockedException()
    : DomainException(
        "USER_BLOCKED",
        "Tu cuenta o dispositivo ha sido bloqueado por un administrador.",
        StatusCodes.Status403Forbidden);

public sealed class PremiumRequiredException()
    : DomainException(
        "PREMIUM_REQUIRED",
        "Adjuntar foto del ticket está reservado exclusivamente para la versión Premium.",
        StatusCodes.Status403Forbidden);

public sealed class DomainValidationException(string code, string message)
    : DomainException(code, message, StatusCodes.Status400BadRequest);
