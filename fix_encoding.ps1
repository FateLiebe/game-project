$files = @(
    'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController.cs',
    'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Movement.cs',
    'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Combat.cs',
    'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Skills.cs'
)
$win1252 = [System.Text.Encoding]::GetEncoding(1252)
$utf8 = [System.Text.Encoding]::UTF8

foreach ($f in $files) {
    if (Test-Path $f) {
        $corrupted = [System.IO.File]::ReadAllText($f, $utf8)
        $fixedBytes = $win1252.GetBytes($corrupted)
        $fixedText = $utf8.GetString($fixedBytes)
        [System.IO.File]::WriteAllText($f, $fixedText, $utf8)
        Write-Host "Fixed $f"
    }
}
