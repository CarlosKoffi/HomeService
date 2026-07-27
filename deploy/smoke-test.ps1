[CmdletBinding()]
param(
    [string]$BaseUrl = $env:SMOKE_API_BASE_URL,
    [string]$Username = $env:SMOKE_SITE_AUTH_USERNAME,
    [string]$Password = $env:SMOKE_SITE_AUTH_PASSWORD,
    [int]$StartupDelaySeconds = $(if ($env:SMOKE_STARTUP_DELAY_SECONDS) { [int]$env:SMOKE_STARTUP_DELAY_SECONDS } else { 0 })
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    Write-Host "SMOKE_API_BASE_URL is empty. Skipping deployed API smoke tests."
    exit 0
}

if ($StartupDelaySeconds -gt 0) {
    Write-Host "Waiting $StartupDelaySeconds second(s) before smoke tests..."
    Start-Sleep -Seconds $StartupDelaySeconds
}

$base = $BaseUrl.TrimEnd("/")
$headers = @{}

if (-not [string]::IsNullOrWhiteSpace($Username) -and -not [string]::IsNullOrWhiteSpace($Password)) {
    $token = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${Username}:${Password}"))
    $headers["Authorization"] = "Basic $token"
}

$checks = @(
    @{ Name = "API health"; Path = "/health"; MinCount = 0 },
    @{ Name = "Service catalog"; Path = "/api/services"; MinCount = 1 },
    @{ Name = "Company CMS"; Path = "/api/cms/company/home"; MinCount = 0 },
    @{ Name = "Provider CMS"; Path = "/api/cms/provider/home"; MinCount = 0 },
    @{ Name = "Provider onboarding options"; Path = "/api/provider-onboarding/options"; MinCount = 1 },
    @{ Name = "Provider onboarding companies"; Path = "/api/provider-onboarding/companies"; MinCount = 0 },
    @{ Name = "Admin company applications"; Path = "/api/admin/company-applications"; MinCount = 0 },
    @{ Name = "Admin missions"; Path = "/api/admin/missions"; MinCount = 0 },
    @{ Name = "Admin mission settings"; Path = "/api/admin/mission-settings"; MinCount = 0 },
    @{ Name = "Admin notifications"; Path = "/api/admin/notifications"; MinCount = 0 },
    @{
        Name = "Admin notification delivery rules"
        Path = "/api/admin/notification-delivery-rules"
        MinCount = 1
        RequiredEventKeys = @(
            "MissionAssignedToProvider",
            "MissionAdditionalQuoteRequested",
            "MissionAdditionalQuotePaid",
            "MissionDisputeResolved",
            "MissionRefundApproved")
    },
    @{
        Name = "Admin notification templates"
        Path = "/api/admin/notification-templates"
        MinCount = 1
        RequiredEventKeys = @(
            "MissionAssignedToProvider",
            "MissionAdditionalQuoteAvailable",
            "MissionAdditionalQuotePaid",
            "MissionDisputeResolvedCustomer",
            "MissionRefundApproved")
        RequiredTemplates = @(
            @{ EventKey = "CompanyActivationLinkCreated"; Channel = "Portal" },
            @{ EventKey = "CompanyApplicationNeedsChanges"; Channel = "Portal" },
            @{ EventKey = "MissionAssignedToProvider"; Channel = "MobilePush" },
            @{ EventKey = "MissionAdditionalQuoteAvailable"; Channel = "MobilePush" },
            @{ EventKey = "MissionAdditionalQuotePaid"; Channel = "Portal" },
            @{ EventKey = "MissionDisputeResolvedCustomer"; Channel = "MobilePush" },
            @{ EventKey = "MissionRefundApproved"; Channel = "MobilePush" },
            @{ EventKey = "MissionPaymentTransferred"; Channel = "Portal" })
    },
    @{ Name = "Admin payments"; Path = "/api/admin/payments"; MinCount = 0 },
    @{ Name = "Admin access control"; Path = "/api/admin/access-control"; MinCount = 0 }
)

function Get-Items($payload) {
    if ($null -eq $payload) {
        return @()
    }

    if ($payload -is [array]) {
        return @($payload)
    }

    foreach ($propertyName in @("items", "Items", "notifications", "Notifications", "missions", "Missions", "applications", "Applications", "rules", "Rules", "templates", "Templates")) {
        if ($payload.PSObject.Properties.Name -contains $propertyName) {
            $value = $payload.$propertyName
            if ($value -is [array]) {
                return @($value)
            }
        }
    }

    return @($payload)
}

function Get-ItemCount($payload) {
    return (Get-Items $payload).Count
}

function Test-RequiredEventKeys($check, $payload) {
    if (-not ($check.ContainsKey("RequiredEventKeys"))) {
        return @()
    }

    $items = Get-Items $payload
    $available = @{}
    foreach ($item in $items) {
        if ($item.PSObject.Properties.Name -contains "eventKey") {
            $available[[string]$item.eventKey] = $true
        }
        elseif ($item.PSObject.Properties.Name -contains "EventKey") {
            $available[[string]$item.EventKey] = $true
        }
    }

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($eventKey in $check.RequiredEventKeys) {
        if (-not $available.ContainsKey($eventKey)) {
            $missing.Add($eventKey)
        }
    }

    return @($missing)
}

function Get-ItemPropertyValue($item, [string[]]$propertyNames) {
    foreach ($propertyName in $propertyNames) {
        if ($item.PSObject.Properties.Name -contains $propertyName) {
            return [string]$item.$propertyName
        }
    }

    return $null
}

function Test-RequiredTemplates($check, $payload) {
    if (-not ($check.ContainsKey("RequiredTemplates"))) {
        return @()
    }

    $items = Get-Items $payload
    $available = @{}
    foreach ($item in $items) {
        $eventKey = Get-ItemPropertyValue $item @("eventKey", "EventKey")
        $channel = Get-ItemPropertyValue $item @("channel", "Channel")

        if (-not [string]::IsNullOrWhiteSpace($eventKey) -and -not [string]::IsNullOrWhiteSpace($channel)) {
            $available["$eventKey|$channel"] = $true
        }
    }

    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($template in $check.RequiredTemplates) {
        $key = "$($template.EventKey)|$($template.Channel)"
        if (-not $available.ContainsKey($key)) {
            $missing.Add($key)
        }
    }

    return @($missing)
}

$failures = New-Object System.Collections.Generic.List[string]

foreach ($check in $checks) {
    $url = "$base$($check.Path)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -Headers $headers -TimeoutSec 20
        $status = [int]$response.StatusCode
        if ($status -lt 200 -or $status -ge 300) {
            $failures.Add("$($check.Name) returned HTTP $status on $url")
            continue
        }

        $count = 0
        $json = $null
        if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
            try {
                $json = $response.Content | ConvertFrom-Json
                $count = Get-ItemCount $json
            }
            catch {
                $count = 1
            }
        }

        if ($check.MinCount -gt 0 -and $count -lt $check.MinCount) {
            $failures.Add("$($check.Name) returned $count item(s), expected at least $($check.MinCount).")
            continue
        }

        $missingEventKeys = Test-RequiredEventKeys $check $json
        if ($missingEventKeys.Count -gt 0) {
            $failures.Add("$($check.Name) is missing event key(s): $($missingEventKeys -join ', ').")
            continue
        }

        $missingTemplates = Test-RequiredTemplates $check $json
        if ($missingTemplates.Count -gt 0) {
            $failures.Add("$($check.Name) is missing template(s): $($missingTemplates -join ', ').")
            continue
        }

        Write-Host "[OK] $($check.Name) ($status, count=$count)"
    }
    catch {
        $failures.Add("$($check.Name) failed on $url - $($_.Exception.Message)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "Smoke tests failed:"
    foreach ($failure in $failures) {
        Write-Host "- $failure"
    }
    exit 1
}

Write-Host ""
Write-Host "Deployed API smoke tests passed."
