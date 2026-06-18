namespace CdnFileService.Application.Interfaces;

public record FileValidationResult(bool IsValid, string? Error)
{
    public static readonly FileValidationResult Valid = new(true, null);
    public static FileValidationResult Invalid(string error) => new(false, error);
}

/// <summary>Enforces extension whitelist/blacklist, size limit and basic MIME checks.</summary>
public interface IFileValidator
{
    FileValidationResult Validate(string fileName, long size, string? mimeType);

    bool IsImage(string fileName);
}
