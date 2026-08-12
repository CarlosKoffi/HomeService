[CmdletBinding()]
param(
    [string]$ApiBaseUrl = $(if ($env:EXTERNAL_API_BASE_URL) { $env:EXTERNAL_API_BASE_URL } else { "https://api.wele.africa" }),
    [string]$R2PublicObjectUrl = $env:EXTERNAL_R2_PUBLIC_OBJECT_URL,
    [string]$GooglePlacesQuery = $(if ($env:EXTERNAL_GOOGLE_PLACES_QUERY) { $env:EXTERNAL_GOOGLE_PLACES_QUERY } else { "Plateau Abidjan" }),
    [string]$FirebaseCredentialsBase64 = $env:FIREBASE_CREDENTIALS_BASE64,
    [string]$FirebaseProjectId = $env:FIREBASE_PROJECT_ID,
    [string]$FirebaseValidationDeviceToken = $env:FIREBASE_VALIDATION_DEVICE_TOKEN,
    [string]$JekoApiBaseUrl = $(if ($env:JEKO_API_BASE_URL) { $env:JEKO_API_BASE_URL } else { "https://api.jeko.africa" }),
    [string]$JekoApiKey = $env:JEKO_API_KEY,
    [string]$JekoApiKeyId = $env:JEKO_API_KEY_ID,
    [string]$JekoPaymentRequestId = $env:JEKO_VALIDATION_PAYMENT_REQUEST_ID,
    [switch]$CheckPublic,
    [switch]$CheckFirebase,
    [switch]$CheckJekoReadOnly
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not $CheckPublic -and -not $CheckFirebase -and -not $CheckJekoReadOnly) {
    $CheckPublic = $true
}

function Assert-ConfiguredValue([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name doit etre configure pour lancer ce controle externe."
    }
}

function ConvertTo-Base64Url([byte[]]$Bytes) {
    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-StringValues($Value) {
    if ($null -eq $Value) {
        return
    }
    if ($Value -is [string]) {
        Write-Output $Value
        return
    }
    if ($Value -is [Collections.IDictionary]) {
        foreach ($item in $Value.Values) {
            Get-StringValues $item
        }
        return
    }
    if ($Value -is [Collections.IEnumerable]) {
        foreach ($item in $Value) {
            Get-StringValues $item
        }
        return
    }
    foreach ($property in $Value.PSObject.Properties) {
        Get-StringValues $property.Value
    }
}

function Invoke-PublicServicesProbe {
    Assert-ConfiguredValue "EXTERNAL_API_BASE_URL" $ApiBaseUrl
    $parsedApiBaseUrl = $null
    if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$parsedApiBaseUrl)) {
        throw "EXTERNAL_API_BASE_URL doit etre une URL absolue."
    }

    $base = $ApiBaseUrl.TrimEnd('/')
    $health = Invoke-RestMethod -Uri "$base/health" -Method Get -TimeoutSec 20
    if ($null -eq $health) {
        throw "L'API n'a retourne aucune reponse sur /health."
    }
    Write-Host "[OK] API publique et healthcheck"

    $catalog = Invoke-RestMethod -Uri "$base/api/services" -Method Get -TimeoutSec 30
    if (@($catalog).Count -lt 1) {
        throw "Le catalogue public ne contient aucun service."
    }
    Write-Host "[OK] Catalogue public ($(@($catalog).Count) service(s))"

    $mediaUrl = $R2PublicObjectUrl
    if ([string]::IsNullOrWhiteSpace($mediaUrl)) {
        $mediaUrl = Get-StringValues $catalog |
            Where-Object { $_ -match '^https://media\.wele\.africa/.+' } |
            Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($mediaUrl)) {
        $relativeMediaPath = Get-StringValues $catalog |
            Where-Object { $_ -match '^/(assets|catalog)/.+' } |
            Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($relativeMediaPath)) {
            $mediaUrl = "https://media.wele.africa$relativeMediaPath"
        }
    }
    Assert-ConfiguredValue "EXTERNAL_R2_PUBLIC_OBJECT_URL (ou une URL media.wele.africa dans le catalogue)" $mediaUrl
    $mediaResponse = Invoke-WebRequest `
        -Uri $mediaUrl `
        -Method Get `
        -TimeoutSec 30
    if ($mediaResponse.StatusCode -notin @(200, 206)) {
        throw "Le CDN R2 a retourne HTTP $($mediaResponse.StatusCode)."
    }
    $contentType = [string]$mediaResponse.Headers['Content-Type']
    if (-not $contentType.StartsWith('image/', [StringComparison]::OrdinalIgnoreCase)) {
        throw "L'objet R2 controle n'est pas une image (Content-Type: '$contentType')."
    }
    Write-Host "[OK] Cloudflare R2/CDN public ($contentType)"

    $sessionToken = "ci-$([Guid]::NewGuid().ToString('N'))"
    $encodedQuery = [Uri]::EscapeDataString($GooglePlacesQuery)
    $suggestions = Invoke-RestMethod `
        -Uri "$base/api/public/addresses/autocomplete?query=$encodedQuery&sessionToken=$sessionToken" `
        -Method Get `
        -TimeoutSec 30
    if (@($suggestions).Count -lt 1) {
        throw "Google Places n'a retourne aucune suggestion via l'API Wele. Verifier GOOGLE_PLACES_ENABLED et GOOGLE_PLACES_API_KEY."
    }
    $placeId = [string]@($suggestions)[0].placeId
    Assert-ConfiguredValue "placeId Google Places" $placeId
    $encodedPlaceId = [Uri]::EscapeDataString($placeId)
    $place = Invoke-RestMethod `
        -Uri "$base/api/public/addresses/places/$encodedPlaceId`?sessionToken=$sessionToken" `
        -Method Get `
        -TimeoutSec 30
    if ($null -eq $place -or [string]::IsNullOrWhiteSpace([string]$place.addressLine)) {
        throw "Google Places Details n'a pas retourne d'adresse exploitable."
    }
    Write-Host "[OK] Google Places autocomplete + details"
}

function Get-FirebaseAccessToken {
    Assert-ConfiguredValue "FIREBASE_CREDENTIALS_BASE64" $FirebaseCredentialsBase64
    try {
        $credentialsJson = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($FirebaseCredentialsBase64))
        $credentials = $credentialsJson | ConvertFrom-Json
    }
    catch {
        throw "FIREBASE_CREDENTIALS_BASE64 n'est pas un JSON Firebase Base64 valide."
    }

    Assert-ConfiguredValue "client_email Firebase" ([string]$credentials.client_email)
    Assert-ConfiguredValue "private_key Firebase" ([string]$credentials.private_key)
    $tokenUri = if ([string]::IsNullOrWhiteSpace([string]$credentials.token_uri)) {
        "https://oauth2.googleapis.com/token"
    } else {
        [string]$credentials.token_uri
    }

    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $headerJson = @{ alg = "RS256"; typ = "JWT" } | ConvertTo-Json -Compress
    $payloadJson = @{
        iss = [string]$credentials.client_email
        scope = "https://www.googleapis.com/auth/firebase.messaging"
        aud = $tokenUri
        iat = $now
        exp = $now + 300
    } | ConvertTo-Json -Compress
    $unsignedToken = "$(ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($headerJson))).$(ConvertTo-Base64Url ([Text.Encoding]::UTF8.GetBytes($payloadJson)))"

    $pemPayload = ([string]$credentials.private_key) `
        -replace '-----BEGIN PRIVATE KEY-----', '' `
        -replace '-----END PRIVATE KEY-----', '' `
        -replace '\s', ''
    $privateKeyBytes = [Convert]::FromBase64String($pemPayload)
    $rsa = [Security.Cryptography.RSA]::Create()
    try {
        $bytesRead = 0
        $rsa.ImportPkcs8PrivateKey($privateKeyBytes, [ref]$bytesRead)
        $signature = $rsa.SignData(
            [Text.Encoding]::UTF8.GetBytes($unsignedToken),
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    }
    finally {
        $rsa.Dispose()
    }

    $assertion = "$unsignedToken.$(ConvertTo-Base64Url $signature)"
    $tokenResponse = Invoke-RestMethod `
        -Uri $tokenUri `
        -Method Post `
        -ContentType "application/x-www-form-urlencoded" `
        -Body @{
            grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
            assertion = $assertion
        } `
        -TimeoutSec 30
    Assert-ConfiguredValue "access_token Firebase" ([string]$tokenResponse.access_token)
    return [string]$tokenResponse.access_token
}

function Invoke-FirebaseProbe {
    Assert-ConfiguredValue "FIREBASE_PROJECT_ID" $FirebaseProjectId
    Assert-ConfiguredValue "FIREBASE_VALIDATION_DEVICE_TOKEN" $FirebaseValidationDeviceToken
    $accessToken = Get-FirebaseAccessToken

    $payload = @{
        validate_only = $true
        message = @{
            token = $FirebaseValidationDeviceToken
            data = @{
                type = "ci_validation"
                generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
            }
        }
    } | ConvertTo-Json -Depth 6

    $result = Invoke-RestMethod `
        -Uri "https://fcm.googleapis.com/v1/projects/$FirebaseProjectId/messages:send" `
        -Method Post `
        -Headers @{ Authorization = "Bearer $accessToken" } `
        -ContentType "application/json" `
        -Body $payload `
        -TimeoutSec 30
    if ($null -eq $result -or [string]::IsNullOrWhiteSpace([string]$result.name)) {
        throw "Firebase validate_only n'a pas confirme le message."
    }
    Write-Host "[OK] Firebase OAuth + FCM validate_only (aucune notification envoyee)"
}

function Invoke-JekoReadOnlyProbe {
    Assert-ConfiguredValue "JEKO_API_KEY" $JekoApiKey
    Assert-ConfiguredValue "JEKO_API_KEY_ID" $JekoApiKeyId
    Assert-ConfiguredValue "JEKO_VALIDATION_PAYMENT_REQUEST_ID" $JekoPaymentRequestId
    Assert-ConfiguredValue "JEKO_API_BASE_URL" $JekoApiBaseUrl

    $encodedId = [Uri]::EscapeDataString($JekoPaymentRequestId)
    $result = Invoke-RestMethod `
        -Uri "$($JekoApiBaseUrl.TrimEnd('/'))/partner_api/payment_requests/$encodedId" `
        -Method Get `
        -Headers @{
            "X-API-KEY" = $JekoApiKey
            "X-API-KEY-ID" = $JekoApiKeyId
        } `
        -TimeoutSec 30
    if ($null -eq $result) {
        throw "JEKO n'a retourne aucun statut pour la demande de paiement de reference."
    }
    Write-Host "[OK] JEKO authentification + lecture seule d'un statut de paiement"
}

if ($CheckPublic) {
    Invoke-PublicServicesProbe
}
if ($CheckFirebase) {
    Invoke-FirebaseProbe
}
if ($CheckJekoReadOnly) {
    Invoke-JekoReadOnlyProbe
}

Write-Host "Tous les controles externes demandes sont termines."
