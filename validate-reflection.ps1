# validate-reflection.ps1
# Validates Traverse.Field/Method references against ONI-Decompiled source.
# Catches field renames before you launch the game.
# Usage: powershell -ExecutionPolicy Bypass -File validate-reflection.ps1 [-Verbose]

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $scriptDir "OniAccess"
$decompiledDir = Join-Path $scriptDir "ONI-Decompiled"
$asmDir = Join-Path $decompiledDir "Assembly-CSharp"
$asmFirstpassDir = Join-Path $decompiledDir "Assembly-CSharp-firstpass"

if (-not (Test-Path $asmDir)) {
    Write-Host "ERROR: Decompiled source not found: $asmDir" -ForegroundColor Red
    exit 1
}

# --- Functions ---

$script:sourceContentCache = @{}

function Get-SourceContent {
    param([string]$FilePath)
    if (-not $script:sourceContentCache.ContainsKey($FilePath)) {
        $script:sourceContentCache[$FilePath] = Get-Content -Path $FilePath -Raw
    }
    return $script:sourceContentCache[$FilePath]
}

function Find-DecompiledFile {
    param([string]$TypeName)

    # Strip generic parameters — List<Foo> -> List, ResourceGraph<Tech> -> ResourceGraph
    $TypeName = $TypeName -replace '<.*$', ''

    $path = Join-Path $asmDir "$TypeName.cs"
    if (Test-Path $path) { return $path }

    if (Test-Path $asmFirstpassDir) {
        $path = Join-Path $asmFirstpassDir "$TypeName.cs"
        if (Test-Path $path) { return $path }
    }

    $found = Get-ChildItem -Path $decompiledDir -Recurse -Filter "$TypeName.cs" | Select-Object -First 1
    if ($found) { return $found.FullName }

    # Check nested class index (inner classes that don't have their own file)
    if ($script:nestedClassIndex.ContainsKey($TypeName)) {
        return $script:nestedClassIndex[$TypeName]
    }

    return $null
}

function Test-MemberInFile {
    param([string]$Content, [string]$Name, [string]$Kind)

    $escaped = [regex]::Escape($Name)

    switch ($Kind) {
        "Field" {
            if ($Content -match "(?m)\b$escaped\b\s*[;=]") { return $true }
            if ($Content -match "(?m)\b$escaped\b\s*\{") { return $true }
        }
        "Method" {
            if ($Content -match "(?m)\b$escaped\b\s*[\(<]") { return $true }
        }
    }
    return $false
}

function Get-BaseTypes {
    param([string]$Content)
    $bases = @()
    if ($Content -match '(?m)^\s*(?:public|internal)\s+(?:abstract\s+)?(?:class|struct)\s+\w+(?:<[^>]+>)?\s*:\s*(.+)$') {
        $baseList = $Matches[1] -replace '\{.*$', ''
        foreach ($b in $baseList -split ',') {
            $name = $b.Trim() -replace '<.*$', ''
            if ($name) { $bases += $name }
        }
    }
    return $bases
}

function Test-MemberWithInheritance {
    param([string]$StartFile, [string]$MemberName, [string]$MemberKind)

    $visited = @{}
    $queue = @($StartFile)
    $depth = 0

    # Walk up the inheritance chain
    while ($queue.Count -gt 0 -and $depth -lt 5) {
        $nextQueue = @()
        foreach ($file in $queue) {
            if ($visited.ContainsKey($file)) { continue }
            $visited[$file] = $true

            $content = Get-SourceContent $file
            if (Test-MemberInFile $content $MemberName $MemberKind) {
                return $true
            }

            $bases = Get-BaseTypes $content
            foreach ($baseName in $bases) {
                $baseFile = Find-DecompiledFile $baseName
                if ($baseFile -and -not $visited.ContainsKey($baseFile)) {
                    $nextQueue += $baseFile
                }
            }
        }
        $queue = $nextQueue
        $depth++
    }

    # Not found going up — check pre-built subclass index
    $startTypeName = [System.IO.Path]::GetFileNameWithoutExtension($StartFile)
    if ($script:subclassIndex.ContainsKey($startTypeName)) {
        foreach ($subFile in $script:subclassIndex[$startTypeName]) {
            $raw = Get-SourceContent $subFile
            if (Test-MemberInFile $raw $MemberName $MemberKind) {
                return $true
            }
        }
    }

    return $false
}

# Look up a field's declared type in the decompiled source, walking inheritance.
function Resolve-FieldType {
    param([string]$OwnerType, [string]$FieldName)

    $file = Find-DecompiledFile $OwnerType
    if (-not $file) { return $null }

    $visited = @{}
    $queue = @($file)
    $depth = 0
    $escaped = [regex]::Escape($FieldName)

    while ($queue.Count -gt 0 -and $depth -lt 5) {
        $nextQueue = @()
        foreach ($f in $queue) {
            if ($visited.ContainsKey($f)) { continue }
            $visited[$f] = $true

            $content = Get-SourceContent $f
            # Match: private/protected/public [static] TypeName memberName followed by
            # ; (field), = (field initializer or expression-bodied property), or { (property)
            if ($content -match "(?m)(?:private|protected|public)\s+(?:static\s+)?(\w+)\s+$escaped\s*[;={]") {
                return $Matches[1]
            }

            $bases = Get-BaseTypes $content
            foreach ($baseName in $bases) {
                $baseFile = Find-DecompiledFile $baseName
                if ($baseFile -and -not $visited.ContainsKey($baseFile)) {
                    $nextQueue += $baseFile
                }
            }
        }
        $queue = $nextQueue
        $depth++
    }
    return $null
}

function Resolve-Types {
    param(
        [string]$VarName,
        [hashtable]$VarTypes,
        [string[]]$ScreenTypes,
        [string]$HandlerName
    )

    # _screen or screen with handler context -> use registered screen types
    if ($VarName -eq '_screen' -or $VarName -eq 'screen') {
        if ($ScreenTypes.Count -gt 0) {
            return $ScreenTypes
        }
        # Fall through to $varTypes — the variable may have a typed declaration
    }

    # Typed variable (from parameter, local declaration, or field-type chaining)
    if ($VarTypes.ContainsKey($VarName)) {
        $types = $VarTypes[$VarName] | Where-Object {
            $_ -ne 'var' -and $_ -ne 'object' -and $_ -ne 'KScreen'
        }
        if ($types) { return @($types) }
    }

    return @()
}

# --- Pre-build subclass index and nested class index ---
# subclassIndex: baseType -> [subclass file paths] (direct inheritance only)
# nestedClassIndex: className -> filePath (for inner/nested classes not in their own file)
$script:subclassIndex = @{}
$script:nestedClassIndex = @{}
# Recurse: project-mode decompilation puts namespaced types (Database, Klei.AI,
# TUNING, STRINGS, ...) in subdirectories, not just flat files at the top level.
$indexDirs = @($asmDir)
if (Test-Path $asmFirstpassDir) { $indexDirs += $asmFirstpassDir }
foreach ($f in Get-ChildItem -Path $indexDirs -Recurse -Filter "*.cs") {
    $content = Get-Content -Path $f.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    $firstLines = ($content -split "`n" | Select-Object -First 15) -join ' '
    if ($firstLines -match 'class\s+\w+(?:<[^>]+>)?\s*:\s*(\w+)') {
        $base = $Matches[1]
        if (-not $script:subclassIndex.ContainsKey($base)) {
            $script:subclassIndex[$base] = @()
        }
        $script:subclassIndex[$base] += $f.FullName
    }
    # Index all nested class declarations (class keyword not at file level)
    foreach ($cm in [regex]::Matches($content, '(?m)^\s+(?:public|private|protected|internal)\s+(?:sealed\s+)?class\s+(\w+)')) {
        $nested = $cm.Groups[1].Value
        if (-not $script:nestedClassIndex.ContainsKey($nested)) {
            $script:nestedClassIndex[$nested] = $f.FullName
        }
    }
}

# --- Phase 1: Build handler-to-screen-type map from ContextDetector ---

Write-Host "Building handler-to-screen-type map..."

$contextDetector = Join-Path $sourceDir "Handlers\ContextDetector.cs"
$handlerToScreenTypes = @{}

if (Test-Path $contextDetector) {
    $cdLines = Get-Content -Path $contextDetector
    $lastTypeName = $null

    foreach ($cdLine in $cdLines) {
        if ($cdLine -match 'Register<(\w+)>\(.*new\s+(\w+)\(') {
            $screenType = $Matches[1]
            $handlerType = $Matches[2]
            if (-not $handlerToScreenTypes.ContainsKey($handlerType)) {
                $handlerToScreenTypes[$handlerType] = @()
            }
            $handlerToScreenTypes[$handlerType] += $screenType
            $lastTypeName = $null
        }
        elseif ($cdLine -match 'TypeByName\(\s*"(\w+)"\s*\)') {
            $lastTypeName = $Matches[1]
        }
        elseif ($cdLine -match 'Register\(\s*\w+.*new\s+(\w+)\(' -and $lastTypeName) {
            $handlerType = $Matches[1]
            if (-not $handlerToScreenTypes.ContainsKey($handlerType)) {
                $handlerToScreenTypes[$handlerType] = @()
            }
            $handlerToScreenTypes[$handlerType] += $lastTypeName
            $lastTypeName = $null
        }
    }

    # Parse the foreach block: new[] { "ScreenA", "ScreenB", ... } => new HandlerType(screen)
    $contextContent = Get-Content -Path $contextDetector -Raw
    if ($contextContent -match 'new\[\]\s*\{([^}]+)\}[^}]+new\s+(\w+)\(') {
        $nameList = $Matches[1]
        $handler = $Matches[2]
        foreach ($m in [regex]::Matches($nameList, '"(\w+)"')) {
            $sType = $m.Groups[1].Value
            if (-not $handlerToScreenTypes.ContainsKey($handler)) {
                $handlerToScreenTypes[$handler] = @()
            }
            if ($handlerToScreenTypes[$handler] -notcontains $sType) {
                $handlerToScreenTypes[$handler] += $sType
            }
        }
    }
}

if ($Verbose) {
    foreach ($h in $handlerToScreenTypes.Keys | Sort-Object) {
        Write-Host "  $h -> $($handlerToScreenTypes[$h] -join ', ')" -ForegroundColor DarkGray
    }
}

$totalHandlerMappings = 0
foreach ($v in $handlerToScreenTypes.Values) { $totalHandlerMappings += $v.Count }
Write-Host "Found $totalHandlerMappings handler-to-screen mappings"

# --- Phase 2: Scan for Traverse references ---

Write-Host "Scanning OniAccess/*.cs for Traverse references..."

$csFiles = Get-ChildItem -Path $sourceDir -Recurse -Filter "*.cs"
$references = @()

foreach ($file in $csFiles) {
    $rawContent = Get-Content -Path $file.FullName -Raw

    # Skip files with no Traverse usage at all
    if ($rawContent -notmatch 'Traverse') { continue }

    $content = Get-Content -Path $file.FullName
    $fileName = $file.Name
    $handlerName = $fileName -replace '\.cs$', ''

    # Determine screen types for this handler
    $screenTypes = @()
    if ($handlerToScreenTypes.ContainsKey($handlerName)) {
        $screenTypes = $handlerToScreenTypes[$handlerName]
    }

    # Track Traverse variables: varName -> the variable passed to Traverse.Create()
    $traverseVars = @{}
    # Track typed local variables and parameters: varName -> [list of types]
    # Multiple types per name because the same name may appear in different methods.
    $varTypes = @{}

    # Helper to add a type for a variable (accumulates, no overwrite)
    function Add-VarType {
        param([hashtable]$Map, [string]$Name, [string]$Type)
        if (-not $Map.ContainsKey($Name)) {
            $Map[$Name] = @($Type)
        } elseif ($Map[$Name] -notcontains $Type) {
            $Map[$Name] += $Type
        }
    }

    # --- Extract parameter types from all method/constructor signatures ---
    foreach ($m in [regex]::Matches($rawContent,
        '(?:private|public|protected|internal|static)\s+(?:static\s+)?(?:override\s+)?(?:void|bool|string|int|float|\w+(?:<[^>]+>)?)\s+\w+\s*\(([^)]*)\)',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $paramBlock = $m.Groups[1].Value -replace '\s+', ' '
        foreach ($p in $paramBlock -split ',') {
            $p = $p.Trim()
            if ($p -match '^(\w+(?:<[^>]+>)?)\s+(\w+)$') {
                Add-VarType $varTypes $Matches[2] $Matches[1]
            }
        }
    }
    # Also match constructor signatures: ClassName(TypeName param, ...)
    foreach ($m in [regex]::Matches($rawContent,
        '(?:private|public|protected|internal)\s+\w+\s*\(([^)]+)\)\s*(?::\s*(?:base|this)\s*\([^)]*\)\s*)?\{',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $paramBlock = $m.Groups[1].Value -replace '\s+', ' '
        foreach ($p in $paramBlock -split ',') {
            $p = $p.Trim()
            if ($p -match '^(\w+(?:<[^>]+>)?)\s+(\w+)$') {
                Add-VarType $varTypes $Matches[2] $Matches[1]
            }
        }
    }

    # --- Extract typed local declarations and field declarations ---
    # TypeName varName = ... (locals with initializer)
    foreach ($m in [regex]::Matches($rawContent,
        '(?m)^\s+(\w+(?:<[^>]+>)?)\s+(\w+)\s*=\s*',
        [System.Text.RegularExpressions.RegexOptions]::Multiline)) {
        $typeName = $m.Groups[1].Value
        $varName = $m.Groups[2].Value
        if ($typeName -notin @('var', 'return', 'throw', 'new', 'if', 'else', 'for', 'foreach', 'while', 'switch', 'case', 'try', 'catch', 'finally')) {
            Add-VarType $varTypes $varName $typeName
        }
    }
    # TypeName varName; (uninitialized locals)
    foreach ($m in [regex]::Matches($rawContent,
        '(?m)^\s+(\w+(?:<[^>]+>)?)\s+(\w+)\s*;',
        [System.Text.RegularExpressions.RegexOptions]::Multiline)) {
        $typeName = $m.Groups[1].Value
        $varName = $m.Groups[2].Value
        if ($typeName -notin @('var', 'return', 'throw', 'new', 'if', 'else', 'for', 'foreach', 'while', 'switch', 'case', 'try', 'catch', 'finally', 'using', 'namespace', 'class', 'struct', 'interface', 'enum')) {
            Add-VarType $varTypes $varName $typeName
        }
    }
    # Field declarations: [access] [readonly] TypeName _fieldName;
    foreach ($m in [regex]::Matches($rawContent,
        '(?:private|protected|public|internal)\s+(?:readonly\s+)?(?:static\s+)?(\w+(?:<[^>]+>)?)\s+(_\w+)\s*;')) {
        Add-VarType $varTypes $m.Groups[2].Value $m.Groups[1].Value
    }

    # --- Method return types: var x = MethodName() where method is declared in this file ---
    # Build a map of method name -> return type from declarations in this file
    $methodReturnTypes = @{}
    foreach ($m in [regex]::Matches($rawContent,
        '(?:private|public|protected|internal)\s+(?:static\s+)?(?:override\s+)?(\w+(?:<[^>]+>)?)\s+(\w+)\s*\(',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        $retType = $m.Groups[1].Value
        $methName = $m.Groups[2].Value
        if ($retType -notin @('void', 'bool', 'string', 'int', 'float', 'var')) {
            $methodReturnTypes[$methName] = $retType
        }
    }
    # Apply: var x = MethodName(...) or var x = this.MethodName(...)
    foreach ($m in [regex]::Matches($rawContent,
        '(?:var\s+)(\w+)\s*=\s*(?:this\.)?(\w+)\s*\(')) {
        $vn = $m.Groups[1].Value
        $mn = $m.Groups[2].Value
        if ($methodReturnTypes.ContainsKey($mn)) {
            Add-VarType $varTypes $vn $methodReturnTypes[$mn]
        }
    }

    # --- Property return types: var x = expr.PropertyName where property is TypeName PropertyName => ---
    foreach ($m in [regex]::Matches($rawContent,
        '(?:private|public|protected|internal)\s+(?:static\s+)?(\w+)\s+(\w+)\s*=>')) {
        $propType = $m.Groups[1].Value
        $propName = $m.Groups[2].Value
        if ($propType -notin @('void', 'bool', 'string', 'int', 'float', 'var')) {
            $methodReturnTypes[$propName] = $propType
        }
    }
    # Apply property access: var x = expr.PropertyName;
    foreach ($m in [regex]::Matches($rawContent,
        '(?:var\s+)(\w+)\s*=\s*\w+\.(\w+)\s*;')) {
        $vn = $m.Groups[1].Value
        $pn = $m.Groups[2].Value
        if ($methodReturnTypes.ContainsKey($pn)) {
            Add-VarType $varTypes $vn $methodReturnTypes[$pn]
        }
    }

    # --- GetComponent(AccessTools.TypeByName("TypeName")): runtime type from string ---
    foreach ($m in [regex]::Matches($rawContent,
        '(\w+)\s*=\s*[^;]+\.GetComponent\(\s*(?:HarmonyLib\.)?AccessTools\.TypeByName\(\s*"(\w+)"\s*\)',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
        Add-VarType $varTypes $m.Groups[1].Value $m.Groups[2].Value
    }

    # --- Cross-file property access: var x = fieldVar.PropertyName ---
    # When fieldVar has a known type and PropertyName is defined on that type,
    # resolve the property's return type from the source file.
    foreach ($m in [regex]::Matches($rawContent,
        '(?:var\s+)(\w+)\s*=\s*(\w+)\.(\w+)\s*;')) {
        $vn = $m.Groups[1].Value
        $fieldVar = $m.Groups[2].Value
        $propName = $m.Groups[3].Value
        if ($varTypes.ContainsKey($fieldVar)) {
            foreach ($ft in $varTypes[$fieldVar]) {
                $ftClean = $ft -replace '<.*$', ''
                $typeFile = Find-DecompiledFile $ftClean
                if (-not $typeFile) {
                    # Try mod source files (e.g., CodexScreenHandler)
                    $modFile = Get-ChildItem -Path $sourceDir -Recurse -Filter "$ftClean.cs" | Select-Object -First 1
                    if ($modFile) { $typeFile = $modFile.FullName }
                }
                if ($typeFile) {
                    $typeContent = Get-SourceContent $typeFile
                    $escapedProp = [regex]::Escape($propName)
                    # Match: ReturnType PropertyName => or ReturnType PropertyName {
                    if ($typeContent -match "(?m)(?:internal|public|protected|private)\s+(?:static\s+)?(\w+)\s+$escapedProp\s*(?:=>|\{)") {
                        $propType = $Matches[1]
                        if ($propType -notin @('void', 'bool', 'string', 'int', 'float')) {
                            Add-VarType $varTypes $vn $propType
                        }
                    }
                }
            }
        }
    }

    # --- foreach loop variable type inference: foreach (var x in collection) ---
    # When collection has type List<T> or similar generic, extract T as the element type.
    foreach ($m in [regex]::Matches($rawContent, 'foreach\s*\(\s*(?:var|\w+)\s+(\w+)\s+in\s+(\w+)\s*\)')) {
        $loopVar = $m.Groups[1].Value
        $collVar = $m.Groups[2].Value
        if ($varTypes.ContainsKey($collVar)) {
            foreach ($ct in $varTypes[$collVar]) {
                # Extract element type from List<T>, IList<T>, IEnumerable<T>, etc.
                if ($ct -match '<(\w+(?:\.\w+)*)>') {
                    $elemType = $Matches[1]
                    # Handle qualified names like SimpleInfoScreen.StatusItemEntry -> StatusItemEntry
                    if ($elemType -match '\.(\w+)$') {
                        $elemType = $Matches[1]
                    }
                    Add-VarType $varTypes $loopVar $elemType
                }
            }
        }
    }

    # --- `is` pattern matching: expr is TypeName varName ---
    foreach ($m in [regex]::Matches($rawContent, '\bis\s+(\w+)\s+(\w+)\b')) {
        Add-VarType $varTypes $m.Groups[2].Value $m.Groups[1].Value
    }

    # --- `as` casts: varName = expr as TypeName ---
    foreach ($m in [regex]::Matches($rawContent, '(\w+)\s*=\s*[^;]+\bas\s+(\w+)\s*;')) {
        Add-VarType $varTypes $m.Groups[1].Value $m.Groups[2].Value
    }

    # --- GetComponent/GetComponentInChildren<T>(): varName = expr.GetComponent*<TypeName>() ---
    foreach ($m in [regex]::Matches($rawContent, '(\w+)\s*=\s*[^;]+\.GetComponent\w*<(\w+)>\s*\(')) {
        Add-VarType $varTypes $m.Groups[1].Value $m.Groups[2].Value
    }

    # --- GetValue<ConcreteType>(): varName = expr.GetValue<TypeName>() ---
    foreach ($m in [regex]::Matches($rawContent, '(\w+)\s*=\s*[^;]+\.GetValue<(\w+)>\s*\(')) {
        $tn = $m.Groups[2].Value
        if ($tn -ne 'object') {
            Add-VarType $varTypes $m.Groups[1].Value $tn
        }
    }

    # --- Field<T>().Value: varName = expr.Field<TypeName>("x").Value ---
    foreach ($m in [regex]::Matches($rawContent, '(\w+)\s*=\s*[^;]+\.Field<(\w+)>\s*\([^)]+\)\.Value')) {
        Add-VarType $varTypes $m.Groups[1].Value $m.Groups[2].Value
    }

    # Track Traverse variables that remember which field they came from,
    # for two-step traversal: var tv = Traverse.Create(x).Field("foo"); var y = tv.GetValue<object>()
    $traverseFieldSource = @{}  # traverseVarName -> @{ SourceVar; FieldName }

    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        $lineNum = $i + 1

        # Track Traverse variable assignments: var foo = Traverse.Create(bar)
        if ($line -match '(?:var\s+|Traverse\s+)(\w+)\s*=\s*Traverse\.Create\((\w+)\)') {
            $traverseVars[$Matches[1]] = $Matches[2]
        }

        # Track Traverse.Create(x).Field("foo") assigned to a variable (two-step pattern)
        if ($line -match '(?:var\s+|Traverse\s+)(\w+)\s*=\s*Traverse\.Create\((\w+)\)\.Field\(\s*"([^"]+)"\s*\)') {
            $traverseFieldSource[$Matches[1]] = @{ SourceVar = $Matches[2]; FieldName = $Matches[3] }
        }
        # Also: traverseVar.Field("foo") assigned to a new variable
        if ($line -match '(?:var\s+|Traverse\s+)(\w+)\s*=\s*(\w+)\.Field\(\s*"([^"]+)"\s*\)') {
            $tvName = $Matches[2]
            if ($traverseVars.ContainsKey($tvName)) {
                $traverseFieldSource[$Matches[1]] = @{ SourceVar = $traverseVars[$tvName]; FieldName = $Matches[3] }
            }
        }

        # Two-step traversal: var y = traverseVar.GetValue<object>()
        # Resolve y's type by looking up the field's declared type in decompiled source
        if ($line -match '(?:var|object)\s+(\w+)\s*=\s*(\w+)\.GetValue<object>\(') {
            $assignedVar = $Matches[1]
            $tvName = $Matches[2]
            if ($traverseFieldSource.ContainsKey($tvName)) {
                $src = $traverseFieldSource[$tvName]
                $sourceTypes = Resolve-Types $src.SourceVar $varTypes $screenTypes $handlerName
                foreach ($st in $sourceTypes) {
                    $fieldType = Resolve-FieldType $st $src.FieldName
                    if ($fieldType -and $fieldType -ne 'object' -and $fieldType -ne 'GameObject') {
                        Add-VarType $varTypes $assignedVar $fieldType
                        break
                    }
                }
            }
        }

        # Two-step via Traverse var: var y = traverseVar.Field("x").GetValue<object>()
        # where traverseVar wraps a known-type variable
        if ($line -match '(?:var|object)\s+(\w+)\s*=\s*(\w+)\.Field\(\s*"([^"]+)"\s*\)\.GetValue<object>\(') {
            $assignedVar = $Matches[1]
            $tvName = $Matches[2]
            $fieldName = $Matches[3]
            if ($traverseVars.ContainsKey($tvName)) {
                $wrappedVar = $traverseVars[$tvName]
                $sourceTypes = Resolve-Types $wrappedVar $varTypes $screenTypes $handlerName
                foreach ($st in $sourceTypes) {
                    $fieldType = Resolve-FieldType $st $fieldName
                    if ($fieldType -and $fieldType -ne 'object' -and $fieldType -ne 'GameObject') {
                        Add-VarType $varTypes $assignedVar $fieldType
                        break
                    }
                }
            }
        }

        # Variable alias: var captured = otherVar — propagate otherVar's types
        if ($line -match '^\s*var\s+(\w+)\s*=\s*(\w+)\s*;') {
            $alias = $Matches[1]
            $source = $Matches[2]
            if ($varTypes.ContainsKey($source)) {
                foreach ($t in $varTypes[$source]) {
                    Add-VarType $varTypes $alias $t
                }
            }
        }

        # Track var x = TypeName.Instance; (statement must end there — a chained
        # member access like TypeName.Instance.Member has the member's type, not TypeName)
        if ($line -match '^\s*var\s+(\w+)\s*=\s*(\w+)\.Instance\s*;') {
            Add-VarType $varTypes $Matches[1] $Matches[2]
        }

        # Singleton member access: var x = TypeName.Instance.Member;
        # Resolve Member's declared type on TypeName from decompiled source.
        if ($line -match '^\s*var\s+(\w+)\s*=\s*(\w+)\.Instance\.(\w+)\s*;') {
            $memberType = Resolve-FieldType $Matches[2] $Matches[3]
            if ($memberType -and $memberType -ne 'object') {
                Add-VarType $varTypes $Matches[1] $memberType
            }
        }

        # Single-line chained traversal: var foo = Traverse.Create(bar).Field("x").GetValue<object>()
        if ($line -match '(?:var|object)\s+(\w+)\s*=\s*Traverse\.Create\((\w+)\)\.Field\(\s*"([^"]+)"\s*\)\.GetValue<object>\(') {
            $assignedVar = $Matches[1]
            $sourceVar = $Matches[2]
            $fieldName = $Matches[3]

            $sourceTypes = Resolve-Types $sourceVar $varTypes $screenTypes $handlerName
            foreach ($st in $sourceTypes) {
                $fieldType = Resolve-FieldType $st $fieldName
                if ($fieldType -and $fieldType -ne 'object' -and $fieldType -ne 'GameObject') {
                    Add-VarType $varTypes $assignedVar $fieldType
                    break
                }
            }
        }

        # Pattern 1: Traverse.Create(expr).Field("name") or .Method("name")
        if ($line -match 'Traverse\.Create\((\w+)\)\.(Field|Method)\s*(?:<[^>]+>)?\s*\(\s*"([^"]+)"') {
            $targetVar = $Matches[1]
            $kind = $Matches[2]
            $memberName = $Matches[3]

            $resolved = Resolve-Types $targetVar $varTypes $screenTypes $handlerName
            $references += @{
                File = $file.FullName
                FileName = $fileName
                Line = $lineNum
                MemberName = $memberName
                MemberKind = $kind
                TargetVar = $targetVar
                ResolvedTypes = $resolved
            }
        }

        # Pattern 2: traverseVar.Field("name") where traverseVar was assigned earlier
        elseif ($line -match '(\w+)\.(Field|Method)\s*(?:<[^>]+>)?\s*\(\s*"([^"]+)"') {
            $varName = $Matches[1]
            $kind = $Matches[2]
            $memberName = $Matches[3]

            if ($traverseVars.ContainsKey($varName)) {
                $targetVar = $traverseVars[$varName]
                $resolved = Resolve-Types $targetVar $varTypes $screenTypes $handlerName
                $references += @{
                    File = $file.FullName
                    FileName = $fileName
                    Line = $lineNum
                    MemberName = $memberName
                    MemberKind = $kind
                    TargetVar = $targetVar
                    ResolvedTypes = $resolved
                }
            }
        }
    }
}

Write-Host "Found $($references.Count) Traverse references"
Write-Host ""

# --- Phase 3: Validate against decompiled source ---

Write-Host "Validating against ONI-Decompiled/..."

$okCount = 0
$missingCount = 0
$unresolvedCount = 0
$missingDetails = @()

foreach ($ref in $references) {
    $types = $ref.ResolvedTypes

    if ($types.Count -eq 0) {
        $unresolvedCount++
        if ($Verbose) {
            Write-Host "[SKIP] $($ref.FileName):$($ref.Line) - $($ref.TargetVar).$($ref.MemberName) (type unresolved)" -ForegroundColor DarkGray
        }
        continue
    }

    # Field must exist on at least one of the resolved types
    $found = $false
    $checkedTypes = @()

    foreach ($typeName in $types) {
        $decompiledFile = Find-DecompiledFile $typeName
        if (-not $decompiledFile) {
            if ($Verbose) {
                Write-Host "[SKIP] $typeName not found in ONI-Decompiled" -ForegroundColor DarkGray
            }
            continue
        }

        $checkedTypes += $typeName
        if (Test-MemberWithInheritance $decompiledFile $ref.MemberName $ref.MemberKind) {
            $found = $true
            break
        }
    }

    if ($checkedTypes.Count -eq 0) {
        $unresolvedCount++
        if ($Verbose) {
            Write-Host "[SKIP] $($ref.FileName):$($ref.Line) - no decompiled files for types: $($types -join ', ')" -ForegroundColor DarkGray
        }
        continue
    }

    if ($found) {
        $okCount++
        if ($Verbose) {
            Write-Host "[OK] $($ref.FileName):$($ref.Line) - $($ref.MemberName) ($($ref.MemberKind))" -ForegroundColor Green
        }
    } else {
        $missingCount++
        $detail = "[MISSING] $($ref.FileName):$($ref.Line) - $($ref.MemberKind) `"$($ref.MemberName)`" not found on $($checkedTypes -join ', ')"
        $missingDetails += $detail
        Write-Host $detail -ForegroundColor Red
    }
}

# --- Summary ---

Write-Host ""
$total = $okCount + $missingCount
Write-Host "Validated: $okCount/$total OK" -NoNewline
if ($unresolvedCount -gt 0) {
    Write-Host ", $unresolvedCount skipped (type unresolved)" -NoNewline
}

if ($missingCount -eq 0) {
    Write-Host "" # newline
    Write-Host "All validated references OK." -ForegroundColor Green
} else {
    Write-Host "" # newline
    Write-Host ""
    Write-Host "$missingCount missing:" -ForegroundColor Red
    foreach ($detail in $missingDetails) {
        Write-Host "  $detail" -ForegroundColor Red
    }
}

exit $missingCount
