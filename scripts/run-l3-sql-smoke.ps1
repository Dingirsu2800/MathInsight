[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot 'docker-compose.l3.yml'
$environmentFile = Join-Path $repositoryRoot '.env.l3'
$projectName = 'mathinsight-l3-smoke'
$connectionVariables = @(
    'RECOMMENDER_SQLSERVER_CONNECTION',
    'TESTGEN_SQLSERVER_CONNECTION',
    'QUESTIONBANK_SQLSERVER_CONNECTION'
)

function Get-L3SqlServerPassword {
    param([string]$Path)

    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmedLine = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine) -or $trimmedLine.StartsWith('#')) {
            continue
        }

        $prefix = 'L3_SQLSERVER_SA_PASSWORD='
        if ($trimmedLine.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            $password = $trimmedLine.Substring($prefix.Length)
            if ([string]::IsNullOrWhiteSpace($password)) {
                throw 'L3_SQLSERVER_SA_PASSWORD must not be empty in .env.l3.'
            }

            return $password
        }
    }

    throw 'Set L3_SQLSERVER_SA_PASSWORD in .env.l3 before running L3 smoke tests.'
}

function Restore-ProcessEnvironmentVariable {
    param(
        [string]$Name,
        [bool]$WasSet,
        [AllowNull()][string]$Value
    )

    if ($WasSet) {
        [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    }
    else {
        [Environment]::SetEnvironmentVariable($Name, $null, 'Process')
    }
}

if (-not (Test-Path -LiteralPath $composeFile -PathType Leaf)) {
    throw "Missing L3 Compose file: $composeFile"
}

if (-not (Test-Path -LiteralPath $environmentFile -PathType Leaf)) {
    throw "Missing $environmentFile. Copy .env.l3.example to .env.l3 and set a local SQL Server password."
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI is required to run L3 SQL smoke tests.'
}

$saPassword = Get-L3SqlServerPassword -Path $environmentFile
$connectionStringBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$connectionStringBuilder['Data Source'] = '127.0.0.1,14333'
$connectionStringBuilder['Initial Catalog'] = 'master'
$connectionStringBuilder['User ID'] = 'sa'
$connectionStringBuilder['Password'] = $saPassword
$connectionStringBuilder['Encrypt'] = $false
$connectionStringBuilder['TrustServerCertificate'] = $true
$connectionStringBuilder['Connect Timeout'] = 15
$connectionString = $connectionStringBuilder.ConnectionString

$previousEnvironment = @{}
foreach ($name in $connectionVariables) {
    $previousEnvironment[$name] = [pscustomobject]@{
        WasSet = Test-Path "Env:$name"
        Value  = [Environment]::GetEnvironmentVariable($name, 'Process')
    }
}

$composeArguments = @(
    'compose',
    '--project-name', $projectName,
    '--env-file', $environmentFile,
    '-f', $composeFile
)

$exitCode = 0
$composeWasStarted = $false

try {
    Write-Host 'Starting disposable SQL Server for L3 smoke tests...'
    $composeWasStarted = $true
    & docker @composeArguments up -d --wait
    if ($LASTEXITCODE -ne 0) {
        throw 'SQL Server did not become healthy.'
    }

    foreach ($name in $connectionVariables) {
        [Environment]::SetEnvironmentVariable($name, $connectionString, 'Process')
    }

    $recommenderProject = Join-Path $repositoryRoot 'tests\MathInsight.Modules.Recommender.Tests\MathInsight.Modules.Recommender.Tests.csproj'
    $testGenProject = Join-Path $repositoryRoot 'tests\MathInsight.Modules.TestGen.Tests\MathInsight.Modules.TestGen.Tests.csproj'
    $questionBankProject = Join-Path $repositoryRoot 'tests\MathInsight.Modules.QuestionBank.Tests\MathInsight.Modules.QuestionBank.Tests.csproj'

    Write-Host 'Running Recommender SQL Server smoke test...'
    & dotnet test $recommenderProject --no-restore --filter 'FullyQualifiedName~LectureRecommendationSqlServerSmokeTests|FullyQualifiedName~RecommenderApiSystemTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) {
        throw 'Recommender SQL Server smoke test failed.'
    }

    Write-Host 'Running QuestionBank hosted API SQL Server smoke tests...'
    & dotnet test $questionBankProject --no-restore --filter 'FullyQualifiedName~QuestionBankApiSystemTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) {
        throw 'QuestionBank hosted API SQL Server smoke test failed.'
    }

    Write-Host 'Running TestGen SQL Server smoke tests...'
    & dotnet test $testGenProject --no-restore --filter 'FullyQualifiedName~BlueprintSqlServerSmokeTests|FullyQualifiedName~TopicPracticeSqlServerSmokeTests' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) {
        throw 'TestGen SQL Server smoke tests failed.'
    }
}
catch {
    $exitCode = 1
    Write-Error $_
}
finally {
    foreach ($name in $connectionVariables) {
        $previous = $previousEnvironment[$name]
        Restore-ProcessEnvironmentVariable -Name $name -WasSet $previous.WasSet -Value $previous.Value
    }

    if ($composeWasStarted) {
        Write-Host 'Removing disposable L3 SQL Server service...'
        & docker @composeArguments down --remove-orphans
        if ($LASTEXITCODE -ne 0) {
            Write-Error 'L3 SQL Server cleanup failed. Run docker compose --project-name mathinsight-l3-smoke -f docker-compose.l3.yml down --remove-orphans.'
            if ($exitCode -eq 0) {
                $exitCode = 1
            }
        }
    }
}

exit $exitCode
