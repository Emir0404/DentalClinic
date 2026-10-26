# Preuzima fotografije sa Unsplasha u src\DentalClinic\wwwroot\img.
# Unsplash licenca dozvoljava besplatno korištenje, uključujući komercijalno, bez obaveznog navođenja autora.
# Pokretanje (iz foldera DentalClinic):  powershell -File tools\fetch-images.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root 'src\DentalClinic\wwwroot\img'
New-Item -ItemType Directory -Force $target | Out-Null

Get-Content (Join-Path $PSScriptRoot 'images.tsv') | Where-Object { $_.Trim() } | ForEach-Object {
    $name, $id, $width = $_ -split "`t"
    $file = Join-Path $target "$name.jpg"
    if (Test-Path $file) { Write-Host "preskačem $name (već postoji)"; return }
    $url = "https://images.unsplash.com/photo-${id}?w=${width}&q=75&fm=jpg&fit=max"
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
    Write-Host "preuzeto $name"
}
