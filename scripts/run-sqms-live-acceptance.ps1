[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$acceptanceDatabase = 'workflowengine_sqms_live_acceptance'
$acceptanceConnection = 'Server=DOLLY;Database=workflowengine_sqms_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$smtpLauncher = 'C:\projects\cdt-workflow\.tools\smtp4dev\smtp4dev.cmd'
$smtpDataDirectory = 'C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [scriptblock] $Command,
        [Parameter(Mandatory)] [string] $Description
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Test-LocalPort {
    param([Parameter(Mandatory)] [int] $Port)

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $task = $client.ConnectAsync('localhost', $Port)
        return $task.Wait(500) -and $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

Set-Location -LiteralPath $repositoryRoot

if (-not (Test-Path -LiteralPath $smtpLauncher -PathType Leaf)) {
    throw "smtp4dev launcher was not found at $smtpLauncher."
}
if (Test-Path -LiteralPath $smtpDataDirectory) {
    throw "smtp4dev acceptance data already exists at $smtpDataDirectory. Refusing to overwrite retained evidence."
}
if ((Test-LocalPort -Port 3025) -or (Test-LocalPort -Port 5000)) {
    throw 'Port 3025 or 5000 is already in use. Refusing to stop or replace an unknown process.'
}

$databaseId = (& sqlcmd -S DOLLY -d master -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COALESCE(CONVERT(varchar(20), DB_ID(N'$acceptanceDatabase')), 'NULL');" | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Database preflight failed with exit code $LASTEXITCODE."
}
if ($databaseId -ne 'NULL') {
    throw "Acceptance database already exists with ID $databaseId. Refusing to overwrite retained evidence."
}

$env:ConnectionStrings__DefaultConnection = $acceptanceConnection
$env:Sqms__Enabled = 'true'
$env:Sqms__Links__OpenBiddingDashboardUrl = 'https://sqms-acceptance.example.test/open-bidding?source=live'
$env:Sqms__Links__SellerApprovalBidsDashboardUrl = 'https://sqms-acceptance.example.test/bids?view=seller'
$env:Sqms__Links__BidFormUrl = 'https://sqms-acceptance.example.test/bid?source=live'
$env:Sqms__Scheduling__TimeZoneId = 'Eastern Standard Time'
$env:Sqms__Scheduling__BiddingDays = '15'
$env:Sqms__Scheduling__SellerResponseDays = '5'
$env:Notifications__Sender = 'Smtp'
$env:Smtp__Host = 'localhost'
$env:Smtp__Port = '3025'
$env:Smtp__From = 'workflow-sqms-live@example.test'
$env:Smtp__RecipientOverride = ''

Invoke-Checked -Description 'Acceptance tool build' -Command {
    dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
}
Invoke-Checked -Description 'Console build' -Command {
    dotnet build src/WorkflowEngine.Console/WorkflowEngine.Console.csproj -warnaserror
}
Invoke-Checked -Description 'Database initialization' -Command {
    dotnet run --project src/WorkflowEngine.Console/WorkflowEngine.Console.csproj --no-build -- init-db
}
Invoke-Checked -Description 'Target-table setup' -Command {
    sqlcmd -S DOLLY -d $acceptanceDatabase -E -C -b -i sql/sqms/testing/000_create_sqms_test_tables.sql
}
Invoke-Checked -Description 'SQMS outbox DDL' -Command {
    sqlcmd -S DOLLY -d $acceptanceDatabase -E -C -b -i sql/sqms/001_sqms_notification_outbox_ddl.sql
}
Invoke-Checked -Description 'SQMS metadata DML' -Command {
    sqlcmd -S DOLLY -d $acceptanceDatabase -E -C -b `
        -v 'SqmsOpenToRecipientsJson=["sqms-open.live@example.test"]' `
           'SqmsOpenCcRecipientsJson=["sqms-audit.live@example.test"]' `
           'SqmsSellerApprovalCcRecipientsJson=["sqms-audit.live@example.test"]' `
           'SqmsBidDispositionCcRecipientsJson=["sqms-audit.live@example.test"]' `
        -i sql/sqms/002_sqms_workflow_metadata_dml.sql
}
Invoke-Checked -Description 'SQMS deployment verification' -Command {
    sqlcmd -S DOLLY -d $acceptanceDatabase -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
}
Invoke-Checked -Description 'Generic seed verification' -Command {
    dotnet run --project src/WorkflowEngine.Console/WorkflowEngine.Console.csproj --no-build -- verify-seed
}

$null = New-Item -ItemType Directory -Path $smtpDataDirectory
$smtpOutLog = Join-Path $smtpDataDirectory 'smtp4dev.out.log'
$smtpErrorLog = Join-Path $smtpDataDirectory 'smtp4dev.err.log'
$smtpArguments = @(
    '--smtpport=3025',
    '--urls=http://localhost:5000',
    "--baseappdatapath=$smtpDataDirectory",
    '--db=smtp4dev.db',
    '--nousersettings'
)
$smtpProcess = Start-Process `
    -FilePath $smtpLauncher `
    -ArgumentList $smtpArguments `
    -WorkingDirectory $smtpDataDirectory `
    -RedirectStandardOutput $smtpOutLog `
    -RedirectStandardError $smtpErrorLog `
    -WindowStyle Hidden `
    -PassThru
$smtpProcess.Id | Set-Content -LiteralPath (Join-Path $smtpDataDirectory 'smtp4dev.pid')

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    if ((Test-LocalPort -Port 3025) -and (Test-LocalPort -Port 5000)) {
        break
    }
    Start-Sleep -Milliseconds 500
}
if (-not ((Test-LocalPort -Port 3025) -and (Test-LocalPort -Port 5000))) {
    Get-Content -LiteralPath $smtpOutLog -Tail 100 -ErrorAction SilentlyContinue
    Get-Content -LiteralPath $smtpErrorLog -Tail 100 -ErrorAction SilentlyContinue
    throw 'smtp4dev did not become reachable within 30 seconds.'
}

Invoke-Checked -Description 'Compiled SQMS live acceptance runner' -Command {
    dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build
}
Invoke-Checked -Description 'Independent SQMS live acceptance SQL verifier' -Command {
    sqlcmd -S DOLLY -d $acceptanceDatabase -E -C -b -i sql/sqms/testing/005_verify_sqms_live_acceptance.sql
}

Write-Output "acceptanceDatabase=$acceptanceDatabase"
Write-Output 'runMarker=SQMS-LIVE-20260821'
Write-Output "smtp4devPid=$($smtpProcess.Id)"
Write-Output 'smtp4devUrl=http://localhost:5000'
Write-Output "smtp4devData=$smtpDataDirectory"
Write-Output 'SQMS live acceptance orchestration passed. Evidence has been retained.'
