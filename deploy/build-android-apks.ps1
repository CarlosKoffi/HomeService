[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "android-arm64",
    [string]$OutputDirectory = "artifacts/android",
    [ValidateSet("all", "client", "prestataire", "entreprise")]
    [string]$ApplicationName = "all"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$applications = @(
    [pscustomobject]@{
        Name = "client"
        Project = "src/HomeService.Client.Mobile/HomeService.Client.Mobile.csproj"
        GoogleServices = "src/HomeService.Client.Mobile/google-services.json"
        PackageId = "ci.wele.client"
        Artifact = "Wele-Client-Android.apk"
    },
    [pscustomobject]@{
        Name = "prestataire"
        Project = "src/HomeService.Provider.Mobile/HomeService.Provider.Mobile.csproj"
        GoogleServices = "src/HomeService.Provider.Mobile/google-services.json"
        PackageId = "ci.wele.provider"
        Artifact = "Wele-Prestataire-Android.apk"
    },
    [pscustomobject]@{
        Name = "entreprise"
        Project = "src/HomeService.Company.Mobile/HomeService.Company.Mobile.csproj"
        GoogleServices = "src/HomeService.Company.Mobile/google-services.json"
        PackageId = "ci.wele.enterprise"
        Artifact = "Wele-Entreprise-Android.apk"
    }
)

if ($ApplicationName -ne "all") {
    $applications = @($applications | Where-Object Name -eq $ApplicationName)
}

$signingVariableNames = @(
    "ANDROID_KEYSTORE_BASE64",
    "ANDROID_KEYSTORE_PASSWORD",
    "ANDROID_KEY_ALIAS",
    "ANDROID_KEY_PASSWORD"
)
$configuredSigningVariables = $signingVariableNames.Where({
    -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
})

if ($configuredSigningVariables.Count -notin @(0, $signingVariableNames.Count)) {
    $missing = $signingVariableNames.Where({
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
    throw "Signature Android partiellement configuree. Variables manquantes: $($missing -join ', ')."
}

$useConfiguredSigning = $configuredSigningVariables.Count -eq $signingVariableNames.Count
$keystorePath = $null
$manifestApplications = [Collections.Generic.List[object]]::new()
$mapsKeyConfigured = -not [string]::IsNullOrWhiteSpace($env:GOOGLE_MAPS_ANDROID_API_KEY)

if (-not $mapsKeyConfigured) {
    Write-Warning "GOOGLE_MAPS_ANDROID_API_KEY absent: les APK seront compilés avec la valeur de secours et Google Maps ne fonctionnera pas dans ces artefacts."
}

try {
    if ($useConfiguredSigning) {
        $keystorePath = Join-Path ([IO.Path]::GetTempPath()) "wele-ci-$([Guid]::NewGuid().ToString('N')).keystore"
        try {
            $keystoreBytes = [Convert]::FromBase64String($env:ANDROID_KEYSTORE_BASE64)
        }
        catch {
            throw "ANDROID_KEYSTORE_BASE64 n'est pas une valeur Base64 valide."
        }
        [IO.File]::WriteAllBytes($keystorePath, $keystoreBytes)
    }

    foreach ($application in $applications) {
        $projectPath = Join-Path $repositoryRoot $application.Project
        $googleServicesPath = Join-Path $repositoryRoot $application.GoogleServices
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "Projet Android introuvable: $projectPath"
        }
        if (-not (Test-Path -LiteralPath $googleServicesPath -PathType Leaf)) {
            throw "Configuration Firebase Android introuvable: $googleServicesPath"
        }

        $googleServices = Get-Content -LiteralPath $googleServicesPath -Raw | ConvertFrom-Json
        $firebasePackages = @($googleServices.client | ForEach-Object {
            $_.client_info.android_client_info.package_name
        })
        if ($application.PackageId -notin $firebasePackages) {
            throw "Le fichier $($application.GoogleServices) ne contient pas le package $($application.PackageId)."
        }

        [xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
        $projectPackageId = $projectXml.Project.PropertyGroup.ApplicationId |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 1
        if ($projectPackageId -ne $application.PackageId) {
            throw "ApplicationId inattendu pour $($application.Name): '$projectPackageId'."
        }

        $publishDirectory = Join-Path $outputRoot "publish-$($application.Name)"
        New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

        $publishArguments = @(
            "publish",
            $projectPath,
            "--configuration", $Configuration,
            "--framework", "net9.0-android",
            "--runtime", $RuntimeIdentifier,
            "--output", $publishDirectory,
            "-p:AndroidPackageFormats=apk",
            "-p:AndroidCreatePackagePerAbi=false"
        )

        if (-not [string]::IsNullOrWhiteSpace($env:GOOGLE_MAPS_ANDROID_API_KEY)) {
            $publishArguments += "-p:GoogleMapsApiKey=$env:GOOGLE_MAPS_ANDROID_API_KEY"
        }

        if ($useConfiguredSigning) {
            $publishArguments += @(
                "-p:AndroidKeyStore=true",
                "-p:AndroidSigningKeyStore=$keystorePath",
                "-p:AndroidSigningStorePass=$env:ANDROID_KEYSTORE_PASSWORD",
                "-p:AndroidSigningKeyAlias=$env:ANDROID_KEY_ALIAS",
                "-p:AndroidSigningKeyPass=$env:ANDROID_KEY_PASSWORD"
            )
        }

        Write-Host "Publication APK Android: $($application.Name) ($($application.PackageId))"
        & dotnet @publishArguments
        if ($LASTEXITCODE -ne 0) {
            throw "La publication Android de $($application.Name) a echoue avec le code $LASTEXITCODE."
        }

        $apk = Get-ChildItem -LiteralPath $publishDirectory -Recurse -File -Filter "*-Signed.apk" |
            Sort-Object Length -Descending |
            Select-Object -First 1
        if ($null -eq $apk) {
            throw "Aucun APK signe n'a ete produit pour $($application.Name)."
        }

        $artifactPath = Join-Path $outputRoot $application.Artifact
        Copy-Item -LiteralPath $apk.FullName -Destination $artifactPath -Force
        $artifact = Get-Item -LiteralPath $artifactPath
        $hash = Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256

        $manifestApplications.Add([ordered]@{
            name = $application.Name
            packageId = $application.PackageId
            file = $application.Artifact
            bytes = $artifact.Length
            sha256 = $hash.Hash.ToLowerInvariant()
        })
    }

    $manifest = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        configuration = $Configuration
        runtimeIdentifier = $RuntimeIdentifier
        signing = if ($useConfiguredSigning) { "configured-keystore" } else { "ci-debug-keystore" }
        googleMapsKeyConfigured = $mapsKeyConfigured
        applications = $manifestApplications
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot "build-manifest.json") -Encoding utf8
    Write-Host "$($manifestApplications.Count) APK Android genere(s) dans $outputRoot."
}
finally {
    if ($null -ne $keystorePath -and (Test-Path -LiteralPath $keystorePath -PathType Leaf)) {
        Remove-Item -LiteralPath $keystorePath -Force
    }
}
