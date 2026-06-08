$filePath = "c:\Users\tanhv\Project\HinoTools.Alarm_27092023_Test\HinoTools.Alarm_27092023_Test\scratch\test_user_payload.ps1"
$content = Get-Content $filePath -Raw

# Find the body string
if ($content -match '\$body = "([^"]+)"') {
    $body = $Matches[1]
    
    # Parse query string
    $pairs = $body.Split('&')
    foreach ($pair in $pairs) {
        $eq = $pair.IndexOf('=')
        if ($eq -gt 0) {
            $key = $pair.Substring(0, $eq)
            $val = [System.Net.WebUtility]::UrlDecode($pair.Substring($eq + 1))
            if ($key -like "*bom*") {
                Write-Host ("KEY: " + $key)
                Write-Host ("LENGTH: " + $val.Length)
                # Print non-base64 characters or print characters as codes
                for ($i = 0; $i -lt $val.Length; $i++) {
                    $c = $val[$i]
                    $code = [int]$c
                    if (($code -lt 48 -and $code -ne 43 -and $code -ne 47 -and $code -ne 61) -or 
                        ($code -gt 57 -and $code -lt 65) -or 
                        ($code -gt 90 -and $code -lt 97) -or 
                        ($code -gt 122)) {
                        $msg = "Invalid Base64 Char at index " + $i + " : '" + $c + "' (Code: " + $code + ")"
                        Write-Host $msg -ForegroundColor Red
                    }
                }
            }
        }
    }
} else {
    Write-Host "Could not match body"
}
