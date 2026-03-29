$url = "https://aka.ms/ssmsfullbuild"
$output = "$env:TEMP\SSMS-Setup.exe"
Write-Host "Downloading SSMS..."
try {
    Invoke-WebRequest -Uri $url -OutFile $output -UseBasicParsing -TimeoutSec 300
    Write-Host "Installing SSMS..."
    Start-Process -FilePath $output -ArgumentList "/install", "/quiet", "/norestart" -Wait
    Write-Host "Done!"
} catch {
    Write-Host "Download failed. Please download manually from:"
    Write-Host "https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms"
}
