param(
    [string]$AssetPath = 'Assets/Input/InputSystem_Actions.inputactions'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$full = Join-Path $root $AssetPath
if (-not (Test-Path $full)) { throw "Asset not found: $full" }

$raw = [System.IO.File]::ReadAllText($full)
if ($raw -match '"name": "Drone"') {
    Write-Output 'Drone map already present, nothing to do.'
    exit 0
}

function New-Id { [guid]::NewGuid().ToString() }

$actions = @(
    @{ name = 'Throttle'; type = 'Value'; control = 'Axis'; initial = $true },
    @{ name = 'Yaw'; type = 'Value'; control = 'Axis'; initial = $true },
    @{ name = 'Pitch'; type = 'Value'; control = 'Axis'; initial = $true },
    @{ name = 'Roll'; type = 'Value'; control = 'Axis'; initial = $true },
    @{ name = 'PrimaryAction'; type = 'Button'; control = 'Button'; initial = $false },
    @{ name = 'Gimbal'; type = 'Value'; control = 'Vector2'; initial = $true },
    @{ name = 'Zoom'; type = 'Value'; control = 'Axis'; initial = $true },
    @{ name = 'NightVision'; type = 'Button'; control = 'Button'; initial = $false },
    @{ name = 'MotorStart'; type = 'Button'; control = 'Button'; initial = $false },
    @{ name = 'HoverToggle'; type = 'Button'; control = 'Button'; initial = $false },
    @{ name = 'AngleToggle'; type = 'Button'; control = 'Button'; initial = $false },
    @{ name = 'GimbalReset'; type = 'Button'; control = 'Button'; initial = $false }
)

$bindings = @(
    @{ action = 'Throttle'; name = 'W/S'; path = '1DAxis'; composite = $true; part = $false },
    @{ action = 'Throttle'; name = 'negative'; path = '<Keyboard>/s'; composite = $false; part = $true },
    @{ action = 'Throttle'; name = 'positive'; path = '<Keyboard>/w'; composite = $false; part = $true },
    @{ action = 'Yaw'; name = 'A/D'; path = '1DAxis'; composite = $true; part = $false },
    @{ action = 'Yaw'; name = 'negative'; path = '<Keyboard>/a'; composite = $false; part = $true },
    @{ action = 'Yaw'; name = 'positive'; path = '<Keyboard>/d'; composite = $false; part = $true },
    @{ action = 'Pitch'; name = 'Down/Up'; path = '1DAxis'; composite = $true; part = $false },
    @{ action = 'Pitch'; name = 'negative'; path = '<Keyboard>/downArrow'; composite = $false; part = $true },
    @{ action = 'Pitch'; name = 'positive'; path = '<Keyboard>/upArrow'; composite = $false; part = $true },
    @{ action = 'Roll'; name = 'Left/Right'; path = '1DAxis'; composite = $true; part = $false },
    @{ action = 'Roll'; name = 'negative'; path = '<Keyboard>/leftArrow'; composite = $false; part = $true },
    @{ action = 'Roll'; name = 'positive'; path = '<Keyboard>/rightArrow'; composite = $false; part = $true },
    @{ action = 'Gimbal'; name = 'Q/E/R/F'; path = '2DVector'; composite = $true; part = $false },
    @{ action = 'Gimbal'; name = 'up'; path = '<Keyboard>/r'; composite = $false; part = $true },
    @{ action = 'Gimbal'; name = 'down'; path = '<Keyboard>/f'; composite = $false; part = $true },
    @{ action = 'Gimbal'; name = 'left'; path = '<Keyboard>/q'; composite = $false; part = $true },
    @{ action = 'Gimbal'; name = 'right'; path = '<Keyboard>/e'; composite = $false; part = $true },
    @{ action = 'Zoom'; name = 'X/Z'; path = '1DAxis'; composite = $true; part = $false },
    @{ action = 'Zoom'; name = 'negative'; path = '<Keyboard>/x'; composite = $false; part = $true },
    @{ action = 'Zoom'; name = 'positive'; path = '<Keyboard>/z'; composite = $false; part = $true },
    @{ action = 'PrimaryAction'; name = ''; path = '<Keyboard>/space'; composite = $false; part = $false },
    @{ action = 'NightVision'; name = ''; path = '<Keyboard>/n'; composite = $false; part = $false },
    @{ action = 'MotorStart'; name = ''; path = '<Keyboard>/w'; composite = $false; part = $false },
    @{ action = 'HoverToggle'; name = ''; path = '<Keyboard>/h'; composite = $false; part = $false },
    @{ action = 'AngleToggle'; name = ''; path = '<Keyboard>/m'; composite = $false; part = $false },
    @{ action = 'GimbalReset'; name = ''; path = '<Keyboard>/c'; composite = $false; part = $false }
)

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('        {')
$lines.Add('            "name": "Drone",')
$lines.Add(('            "id": "{0}",' -f (New-Id)))
$lines.Add('            "actions": [')

for ($i = 0; $i -lt $actions.Count; $i++) {
    $a = $actions[$i]
    $lines.Add('                {')
    $lines.Add(('                    "name": "{0}",' -f $a.name))
    $lines.Add(('                    "type": "{0}",' -f $a.type))
    $lines.Add(('                    "id": "{0}",' -f (New-Id)))
    $lines.Add(('                    "expectedControlType": "{0}",' -f $a.control))
    $lines.Add('                    "processors": "",')
    $lines.Add('                    "interactions": "",')
    $initial = if ($a.initial) { 'true' } else { 'false' }
    $lines.Add(('                    "initialStateCheck": {0}' -f $initial))
    $suffix = if ($i -lt $actions.Count - 1) { '                },' } else { '                }' }
    $lines.Add($suffix)
}

$lines.Add('            ],')
$lines.Add('            "bindings": [')

for ($i = 0; $i -lt $bindings.Count; $i++) {
    $b = $bindings[$i]
    $isComposite = if ($b.composite) { 'true' } else { 'false' }
    $isPart = if ($b.part) { 'true' } else { 'false' }
    $groups = if ($b.composite) { '' } else { 'Keyboard&Mouse' }
    $lines.Add('                {')
    $lines.Add(('                    "name": "{0}",' -f $b.name))
    $lines.Add(('                    "id": "{0}",' -f (New-Id)))
    $lines.Add(('                    "path": "{0}",' -f $b.path))
    $lines.Add('                    "interactions": "",')
    $lines.Add('                    "processors": "",')
    $lines.Add(('                    "groups": "{0}",' -f $groups))
    $lines.Add(('                    "action": "{0}",' -f $b.action))
    $lines.Add(('                    "isComposite": {0},' -f $isComposite))
    $lines.Add(('                    "isPartOfComposite": {0}' -f $isPart))
    $suffix = if ($i -lt $bindings.Count - 1) { '                },' } else { '                }' }
    $lines.Add($suffix)
}

$lines.Add('            ]')
$lines.Add('        }')

$block = ($lines -join "`r`n")
$anchor = "        }`r`n    ],`r`n    `"controlSchemes`": ["
$replacement = "        },`r`n$block`r`n    ],`r`n    `"controlSchemes`": ["

if ($raw -notmatch [regex]::Escape('"controlSchemes": [')) { throw 'Anchor not found' }

$anchorPattern = "(?s)\}\s*\r?\n\s*\],\s*\r?\n\s*`"controlSchemes`": \["
$match = [regex]::Match($raw, $anchorPattern)
if (-not $match.Success) { throw 'Anchor pattern not found' }

$updated = $raw.Substring(0, $match.Index) + "},`r`n" + $block + "`r`n    ],`r`n    `"controlSchemes`": ["
$updated += $raw.Substring($match.Index + $match.Length)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($full, $updated, $utf8NoBom)

Write-Output ('Drone map inserted into ' + $AssetPath)
