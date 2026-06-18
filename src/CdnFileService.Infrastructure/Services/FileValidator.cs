using CdnFileService.Application.Common;
using CdnFileService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CdnFileService.Infrastructure.Services;

/// <summary>Extension whitelist/blacklist, size and basic MIME-consistency checks.</summary>
public class FileValidator : IFileValidator
{
    private readonly StorageOptions _options;

    public FileValidator(IOptions<StorageOptions> options) => _options = options.Value;

    public FileValidationResult Validate(string fileName, long size, string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return FileValidationResult.Invalid("File name is required.");

        var ext = NormalizeExt(fileName);
        if (string.IsNullOrEmpty(ext))
            return FileValidationResult.Invalid("File must have an extension.");

        if (_options.BlockedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return FileValidationResult.Invalid($"Extension '.{ext}' is blocked.");

        if (!_options.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return FileValidationResult.Invalid($"Extension '.{ext}' is not allowed.");

        if (size <= 0)
            return FileValidationResult.Invalid("File is empty.");

        if (size > _options.MaxUploadSizeBytes)
            return FileValidationResult.Invalid(
                $"File exceeds the maximum size of {_options.MaxUploadSizeBytes / (1024 * 1024)} MB.");

        // Reject obviously executable declared content types regardless of extension.
        if (!string.IsNullOrWhiteSpace(mimeType) && IsExecutableMime(mimeType))
            return FileValidationResult.Invalid("Declared content type is not permitted.");

        return FileValidationResult.Valid;
    }

    public bool IsImage(string fileName) =>
        _options.ImageExtensions.Contains(NormalizeExt(fileName), StringComparer.OrdinalIgnoreCase);

    private static string NormalizeExt(string fileName) =>
        Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

    private static bool IsExecutableMime(string mime) =>
        mime.Contains("x-msdownload", StringComparison.OrdinalIgnoreCase)
        || mime.Contains("x-msdos-program", StringComparison.OrdinalIgnoreCase)
        || mime.Contains("x-executable", StringComparison.OrdinalIgnoreCase)
        || mime.Contains("application/x-sh", StringComparison.OrdinalIgnoreCase);
}
