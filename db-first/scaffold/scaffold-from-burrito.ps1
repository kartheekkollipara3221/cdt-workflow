param(
    [string] $ConnectionString = "Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False",
    [string] $OutputDir = "db-first/scaffold/generated",
    [string] $Project = "src/WorkflowEngine.Console/WorkflowEngine.Console.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Location).Path
$projectPath = Join-Path $repoRoot $Project
$projectDir = Split-Path -Parent $projectPath
$generatedRoot = Join-Path $repoRoot $OutputDir
New-Item -ItemType Directory -Force -Path $generatedRoot | Out-Null
$projectDirUri = [Uri]((Resolve-Path $projectDir).Path.TrimEnd('\') + '\')
$generatedRootUri = [Uri]((Resolve-Path $generatedRoot).Path.TrimEnd('\') + '\')
$relativeGeneratedRoot = [Uri]::UnescapeDataString($projectDirUri.MakeRelativeUri($generatedRootUri).ToString()).TrimEnd('/')

dotnet tool restore

dotnet tool run dotnet-ef -- dbcontext scaffold `
    $ConnectionString `
    Microsoft.EntityFrameworkCore.SqlServer `
    --project $Project `
    --startup-project $Project `
    --context WorkflowEngineDbFirstReferenceContext `
    --context-dir "$relativeGeneratedRoot/Context" `
    --output-dir "$relativeGeneratedRoot/Entities" `
    --namespace WorkflowEngine.DatabaseFirst.Generated.Entities `
    --context-namespace WorkflowEngine.DatabaseFirst.Generated.Context `
    --no-onconfiguring `
    --force
