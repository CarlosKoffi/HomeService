using Microsoft.AspNetCore.DataProtection;

namespace HomeService.Admin;

public static class PersistentDataProtectionExtensions
{
    public static IServiceCollection AddPersistentDataProtection(this IServiceCollection services, IConfiguration configuration, string applicationName)
    {
        var keysPath = configuration["DataProtection:KeysPath"] ?? configuration["DATA_PROTECTION_KEYS_PATH"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            keysPath = "/app/storage/dataprotection";
        }

        Directory.CreateDirectory(keysPath);
        services
            .AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }
}
