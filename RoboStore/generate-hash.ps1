$manager = "Manager123!"
$admin = "Admin123!"
$sha = [System.Security.Cryptography.SHA256]::Create()
$mBytes = [System.Text.Encoding]::UTF8.GetBytes($manager)
$aBytes = [System.Text.Encoding]::UTF8.GetBytes($admin)
$mHash = [Convert]::ToBase64String($sha.ComputeHash($mBytes))
$aHash = [Convert]::ToBase64String($sha.ComputeHash($aBytes))
Write-Host "Manager123! = $mHash"
Write-Host "Admin123! = $aHash"
$sha.Dispose()
