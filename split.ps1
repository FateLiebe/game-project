$path = 'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController.cs'
$lines = Get-Content $path

$core = @()
$movement = @()
$combat = @()
$skills = @()

$movement += 'using UnityEngine;'
$movement += 'using System.Collections;'
$movement += ''
$movement += 'public partial class PlayerController'
$movement += '{'

$combat += 'using UnityEngine;'
$combat += 'using System.Collections;'
$combat += 'using System.Collections.Generic;'
$combat += ''
$combat += 'public partial class PlayerController'
$combat += '{'

$skills += 'using UnityEngine;'
$skills += 'using System;'
$skills += 'using System.Collections;'
$skills += 'using System.Collections.Generic;'
$skills += ''
$skills += 'public partial class PlayerController'
$skills += '{'

$state = 'core'

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match 'public class PlayerController : BaseEntity') {
        $line = $line -replace 'public class', 'public partial class'
    }

    if ($line -match '#region INPUT & MOVEMENT') { $state = 'movement' }
    elseif ($line -match '#region DODGE & PERFECT DODGE') { $state = 'combat' }
    elseif ($line -match '#region HEALTH & DAMAGE') { $state = 'combat' }
    elseif ($line -match '#region COMBAT & ATTACK') { $state = 'combat' }
    elseif ($line -match '#region MOVEMENT LOGIC EXTENSIONS') { $state = 'movement' }
    elseif ($line -match '#region BOSS DETECTION') { $state = 'skills' }
    elseif ($line -match '#region SUPPORT SKILLS') { $state = 'skills' }
    
    if ($i -eq ($lines.Count - 1) -and $line -match '}') {
        $core += '}'
        continue
    }

    switch ($state) {
        'core' { $core += $line }
        'movement' { $movement += $line }
        'combat' { $combat += $line }
        'skills' { $skills += $line }
    }
}

$movement += '}'
$combat += '}'
$skills += '}'

$core | Out-File $path -Encoding UTF8
$movement | Out-File 'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Movement.cs' -Encoding UTF8
$combat | Out-File 'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Combat.cs' -Encoding UTF8
$skills | Out-File 'c:\Users\Admin\ĐoAnTotNgiep\My project\Assets\_Scripts\Entities\PlayerController_Skills.cs' -Encoding UTF8
