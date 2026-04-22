# refactor-team-to-factiontype.ps1
# Run from the project root: .\refactor-team-to-factiontype.ps1
# This replaces all Team enum references with FactionType across the WarMap scripts.

$ErrorActionPreference = "Stop"

$scriptDir = "Assets\_Project\Scripts"
$warMapDir = "$scriptDir\WarMap"
$coreDir = "$scriptDir\Core"

# All files that use the Team enum
$targetFiles = @(
    "$warMapDir\WarMapManager.cs",
    "$warMapDir\WarMapNode.cs",
    "$warMapDir\TokenSystem.cs",
    "$warMapDir\BattleParameters.cs",
    "$warMapDir\BattleManager.cs",
    "$warMapDir\BattleSceneBridge.cs",
    "$warMapDir\Squad.cs",
    "$warMapDir\PlayerSquadManager.cs",
    "$warMapDir\NodeOccupancy.cs",
    "$warMapDir\CaptureController.cs",
    "$warMapDir\WarMapTestHarness.cs",
    "$warMapDir\BattleLobby.cs",
    "$warMapDir\BattleIntegration.cs",
    "$warMapDir\BattleUI.cs",
    "$warMapDir\FPSLauncher.cs",
    "$warMapDir\WarMapUI.cs",
    "$coreDir\SimpleTeamManager.cs"
)

$replacements = @(
    # Enum values (most specific first)
    @("Team.Blue",  "FactionType.Blue"),
    @("Team.Red",   "FactionType.Red"),
    @("Team.Green", "FactionType.Green"),
    @("Team.None",  "FactionType.None"),

    # Generic type parameters
    @("Dictionary<Team,",          "Dictionary<FactionType,"),
    @("Dictionary<int, Team>",     "Dictionary<int, FactionType>"),
    @("HashSet<Team>",             "HashSet<FactionType>"),
    @("List<Team>",                "List<FactionType>"),
    @("Action<Team>",              "Action<FactionType>"),
    @("Action<Team,",              "Action<FactionType,"),
    @(", Team>",                   ", FactionType>"),
    @(", Team,",                   ", FactionType,"),
    @("Action<WarMapNode, Team>",  "Action<WarMapNode, FactionType>"),

    # Cast expressions
    @("(Team)",     "(FactionType)"),
    @("(int)Team",  "(int)FactionType"),

    # Nullable
    @("Team?",   "FactionType?"),

    # Type declarations (field, parameter, return types)
    # These use word boundary matching via regex below
)

# Regex replacements for type declarations
$regexReplacements = @(
    # Standalone "Team " as a type (followed by a word char = variable name)
    @("\bTeam (\w)",  "FactionType `$1"),
    # "Team>" in generics that weren't caught above  
    @("\bTeam>",      "FactionType>"),
    # "<Team>" standalone
    @("<Team>",       "<FactionType>"),
    # "new[] { Team." 
    @("new\[\] \{ Team\.", "new[] { FactionType."),
    # Array: "Team[]"
    @("\bTeam\[\]",   "FactionType[]")
)

$totalChanges = 0

foreach ($filePath in $targetFiles) {
    if (-not (Test-Path $filePath)) {
        Write-Host "  SKIP (not found): $filePath" -ForegroundColor Yellow
        continue
    }

    $content = Get-Content $filePath -Raw -Encoding UTF8
    $original = $content

    # Simple string replacements
    foreach ($pair in $replacements) {
        $content = $content.Replace($pair[0], $pair[1])
    }

    # Regex replacements
    foreach ($pair in $regexReplacements) {
        $content = [regex]::Replace($content, $pair[0], $pair[1])
    }

    # Fix any double-replacements
    $content = $content.Replace("FactionType FactionType", "FactionType")
    $content = $content.Replace("FactionTypeFactionType", "FactionType")

    if ($content -ne $original) {
        Set-Content $filePath -Value $content -Encoding UTF8 -NoNewline
        $changes = ($original.Split("`n").Count - ($content.Split("`n").Count - $content.Split("`n").Count)) 
        Write-Host "  UPDATED: $filePath" -ForegroundColor Green
        $totalChanges++
    } else {
        Write-Host "  NO CHANGES: $filePath" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== REFACTORING COMPLETE ===" -ForegroundColor Cyan
Write-Host "Files modified: $totalChanges" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open Unity and check for compilation errors"
Write-Host "  2. If clean, delete this script"
Write-Host "  3. Commit: git add -A && git commit -m 'Refactor: Unify Team -> FactionType'"
