# Fix namespaces in evaluator files
$evaluatorFiles = @(
    "Core/Evaluation/IEvaluator.cs",
    "Core/Evaluation/ToolSelectionEvaluator.cs",
    "Core/Evaluation/ParameterExtractionEvaluator.cs",
    "Core/Evaluation/ContextRetentionEvaluator.cs",
    "Core/Evaluation/ResponseQualityEvaluator.cs",
    "Core/Evaluation/EvaluatorOrchestrator.cs"
)

$evaluatorUsings = @"
using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Expectations;
using Hermes.Evals.Core.Models.Results;
using Hermes.Evals.Core.Models.Scoring;
"@

foreach ($file in $evaluatorFiles) {
    $path = "C:\dev\repos\Hermes\Hermes.Evals\$file"
    $content = Get-Content -Path $path -Raw

    # Find the namespace line
    if ($content -match "namespace (.+);") {
        $namespace = $matches[1]
        $content = $content -replace "namespace $namespace;", "$evaluatorUsings`nnamespace $namespace;"
        Set-Content -Path $path -Value $content -NoNewline
        Write-Host "Updated $file"
    }
}

# Fix execution files
$executionFiles = @(
    "Core/Execution/IEvaluationEngine.cs",
    "Core/Execution/EvaluationEngine.cs",
    "Core/Execution/ConversationRunner.cs"
)

$executionUsings = @"
using Hermes.Evals.Core.Models.Scenario;
using Hermes.Evals.Core.Models.Results;
"@

foreach ($file in $executionFiles) {
    $path = "C:\dev\repos\Hermes\Hermes.Evals\$file"
    $content = Get-Content -Path $path -Raw

    if ($content -match "namespace (.+);") {
        $namespace = $matches[1]
        $content = $content -replace "namespace $namespace;", "$executionUsings`nnamespace $namespace;"
        Set-Content -Path $path -Value $content -NoNewline
        Write-Host "Updated $file"
    }
}

# Fix ScenarioLoader
$loaderPath = "C:\dev\repos\Hermes\Hermes.Evals\Scenarios\ScenarioLoader.cs"
$loaderContent = Get-Content -Path $loaderPath -Raw
$loaderUsings = @"
using Hermes.Evals.Core.Models.Scenario;
"@

if ($loaderContent -match "namespace (.+);") {
    $namespace = $matches[1]
    $loaderContent = $loaderContent -replace "namespace $namespace;", "$loaderUsings`nnamespace $namespace;"
    Set-Content -Path $loaderPath -Value $loaderContent -NoNewline
    Write-Host "Updated ScenarioLoader.cs"
}

Write-Host "Done fixing namespaces!"
