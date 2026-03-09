[CmdletBinding()]
param(
    [ValidateSet("up", "down", "logs", "ps", "config", "pull")]
    [string]$Action = "up",
    [string]$RepoUrl = "https://github.com/asmulever/TaskTracker.MVP.git",
    [string]$RepoBranch = "main",
    [string]$RepoFolderName = "tasktracker-mvp-src",
    [string]$ComposeProjectName = "tasktracker_mvp",
    [string]$AppPort = "8080",
    [string]$SqlPort = "14333",
    [string]$SaPassword = "TaskTracker123!ChangeMe",
    [int]$StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"

$BaseDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$RepoPath = Join-Path $BaseDirectory $RepoFolderName
$SrcPath = Join-Path $RepoPath "src"
$DockerWorkspacePath = Join-Path $BaseDirectory ".tasktracker-docker"
$DockerfilePath = Join-Path $DockerWorkspacePath "Dockerfile"
$ComposeFilePath = Join-Path $DockerWorkspacePath "compose.yml"
$AppImageName = "${ComposeProjectName}-app:latest"

# Prints a step marker to make the script output easier to scan while it works.
function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Stops execution when a required executable is not available in PATH.
function Assert-CommandAvailable {
    param([string]$CommandName)

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "No se encontro el comando requerido: $CommandName"
    }
}

# Executes an external command and stops the script when the exit code is not zero.
function Invoke-CheckedCommand {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "El comando fallo: $FilePath $($Arguments -join ' ')"
    }
}

# Returns whether docker compose plugin is available on the current machine.
function Test-DockerComposePlugin {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        return $false
    }

    & docker compose version *> $null
    return $LASTEXITCODE -eq 0
}

# Invokes docker compose using either the docker plugin or the legacy docker-compose binary.
function Invoke-Compose {
    param([string[]]$Arguments)

    if (Test-DockerComposePlugin) {
        & docker compose -p $ComposeProjectName -f $ComposeFilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose devolvio un error."
        }
        return
    }

    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        & docker-compose -p $ComposeProjectName -f $ComposeFilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "docker-compose devolvio un error."
        }
        return
    }

    throw "Docker Compose no esta disponible en este equipo."
}

# Clones the target repository if needed or updates the existing local clone to the requested branch.
function Ensure-Repository {
    Assert-CommandAvailable "git"

    if (-not (Test-Path $RepoPath)) {
        Write-Step "Clonando repo desde $RepoUrl"
        Invoke-CheckedCommand "git" @(
            "clone",
            "--branch", $RepoBranch,
            "--single-branch",
            $RepoUrl,
            $RepoPath
        )
        return
    }

    if (-not (Test-Path (Join-Path $RepoPath ".git"))) {
        throw "La carpeta $RepoPath existe pero no es un repositorio Git valido."
    }

    Write-Step "Actualizando repo local"
    Invoke-CheckedCommand "git" @("-C", $RepoPath, "fetch", "origin", $RepoBranch, "--prune")
    Invoke-CheckedCommand "git" @("-C", $RepoPath, "checkout", $RepoBranch)
    Invoke-CheckedCommand "git" @("-C", $RepoPath, "pull", "--ff-only", "origin", $RepoBranch)
}

# Generates the Dockerfile and docker compose file used by the portable launcher.
function Ensure-DockerArtifacts {
    New-Item -ItemType Directory -Force -Path $DockerWorkspacePath | Out-Null

    $composeInitSqlPath = "../$RepoFolderName/src/backend/database/init.sql:/init/init.sql:ro"

    $dockerfileContent = @'
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY TaskTracker.sln ./
COPY backend/TaskTracker.Api/TaskTracker.Api.csproj backend/TaskTracker.Api/
COPY backend/TaskTracker.Application/TaskTracker.Application.csproj backend/TaskTracker.Application/
COPY backend/TaskTracker.Domain/TaskTracker.Domain.csproj backend/TaskTracker.Domain/
COPY backend/TaskTracker.Infrastructure/TaskTracker.Infrastructure.csproj backend/TaskTracker.Infrastructure/

RUN dotnet restore backend/TaskTracker.Api/TaskTracker.Api.csproj

COPY backend ./backend
COPY frontend ./frontend

RUN dotnet publish backend/TaskTracker.Api/TaskTracker.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

EXPOSE 8080

COPY --from=build /app/publish ./
COPY --from=build /src/frontend/task-tracker-ui /frontend/task-tracker-ui

ENTRYPOINT ["dotnet", "TaskTracker.Api.dll"]
'@

    $composeTemplate = @'
services:
  app:
    image: "${TASKTRACKER_APP_IMAGE:-tasktracker_mvp-app:latest}"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__TaskTrackerDb: "Server=sqlserver,1433;Database=TaskTrackerDb;User Id=sa;Password=${TASKTRACKER_SA_PASSWORD:-TaskTracker123!ChangeMe};TrustServerCertificate=True;Encrypt=False"
    depends_on:
      sqlserver:
        condition: service_healthy
      db-init:
        condition: service_completed_successfully
    ports:
      - "${TASKTRACKER_APP_PORT:-8080}:8080"
    restart: unless-stopped

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${TASKTRACKER_SA_PASSWORD:-TaskTracker123!ChangeMe}"
    ports:
      - "${TASKTRACKER_SQL_PORT:-14333}:1433"
    volumes:
      - tasktracker_sqlserver_data:/var/opt/mssql
    healthcheck:
      test:
        [
          "CMD-SHELL",
          "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -Q \"SELECT 1\" -C >/dev/null 2>&1 || exit 1"
        ]
      interval: 10s
      timeout: 5s
      retries: 20
      start_period: 20s
    restart: unless-stopped

  db-init:
    image: mcr.microsoft.com/mssql/server:2022-latest
    depends_on:
      sqlserver:
        condition: service_healthy
    environment:
      MSSQL_SA_PASSWORD: "${TASKTRACKER_SA_PASSWORD:-TaskTracker123!ChangeMe}"
    volumes:
      - "__INIT_SQL_PATH__"
    entrypoint:
      - /bin/bash
      - -lc
    command:
      - >
        for attempt in {1..30}; do
          /opt/mssql-tools18/bin/sqlcmd
          -S sqlserver,1433
          -U sa
          -P "$${MSSQL_SA_PASSWORD}"
          -C
          -Q "SELECT 1" >/dev/null 2>&1 && break;
          sleep 2;
        done;
        /opt/mssql-tools18/bin/sqlcmd
        -S sqlserver,1433
        -U sa
        -P "$${MSSQL_SA_PASSWORD}"
        -C
        -b
        -i /init/init.sql
    restart: "no"

volumes:
  tasktracker_sqlserver_data:
'@

    $composeContent = $composeTemplate.Replace("__INIT_SQL_PATH__", $composeInitSqlPath)

    Set-Content -Path $DockerfilePath -Value $dockerfileContent -Encoding UTF8
    Set-Content -Path $ComposeFilePath -Value $composeContent -Encoding UTF8
}

# Publishes the current runtime parameters to compose through process environment variables.
function Set-ComposeEnvironment {
    $env:TASKTRACKER_APP_IMAGE = $AppImageName
    $env:TASKTRACKER_APP_PORT = $AppPort
    $env:TASKTRACKER_SQL_PORT = $SqlPort
    $env:TASKTRACKER_SA_PASSWORD = $SaPassword
}

# Builds the application image from the cloned repository source code.
function Build-AppImage {
    Assert-CommandAvailable "docker"

    if (-not (Test-Path $SrcPath)) {
        throw "No se encontro la carpeta src del repo en $SrcPath"
    }

    Write-Step "Construyendo imagen Docker de la aplicacion"
    Invoke-CheckedCommand "docker" @(
        "build",
        "-t", $AppImageName,
        "-f", $DockerfilePath,
        $SrcPath
    )
}

# Prints compose status and key service logs after a startup failure.
function Show-StartupDiagnostics {
    Write-Step "Diagnostico de arranque"

    try {
        Invoke-Compose @("ps")
    }
    catch {
        Write-Warning "No se pudo obtener el estado de docker compose."
    }

    try {
        Invoke-Compose @("logs", "--tail", "200", "sqlserver", "db-init")
    }
    catch {
        Write-Warning "No se pudieron obtener los logs de sqlserver/db-init."
    }
}

# Waits until the application responds through the published host port.
function Wait-ForApp {
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $url = "http://localhost:$AppPort/tasks"
    $requestParameters = @{
        Uri = $url
        Method = "Get"
        TimeoutSec = 10
    }

    if ($PSVersionTable.PSVersion.Major -lt 6) {
        $requestParameters["UseBasicParsing"] = $true
    }

    Write-Step "Esperando disponibilidad de la app en $url"

    while ((Get-Date) -lt $deadline) {
        try {
            Invoke-WebRequest @requestParameters | Out-Null
            return
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }

    throw "La app no respondio dentro de $StartupTimeoutSeconds segundos en $url"
}

# Builds the image, starts the full stack and waits for a healthy application endpoint.
function Start-Stack {
    Ensure-Repository
    Ensure-DockerArtifacts
    Set-ComposeEnvironment
    Build-AppImage

    Write-Step "Levantando stack Docker"
    try {
        Invoke-Compose @("up", "-d")
    }
    catch {
        Show-StartupDiagnostics
        throw
    }

    Wait-ForApp
    Invoke-Compose @("ps")

    Write-Host ""
    Write-Host "Stack listo." -ForegroundColor Green
    Write-Host "- App: http://localhost:$AppPort"
    Write-Host "- Swagger: http://localhost:$AppPort/swagger"
    Write-Host "- SQL Server: localhost,$SqlPort"
}

# Stops the compose stack created by this launcher.
function Stop-Stack {
    Ensure-DockerArtifacts
    Set-ComposeEnvironment
    Write-Step "Bajando stack Docker"
    Invoke-Compose @("down", "--remove-orphans")
}

# Streams stack logs to simplify troubleshooting after startup.
function Show-Logs {
    Ensure-DockerArtifacts
    Set-ComposeEnvironment
    Invoke-Compose @("logs", "-f")
}

# Prints the current compose service status.
function Show-Status {
    Ensure-DockerArtifacts
    Set-ComposeEnvironment
    Invoke-Compose @("ps")
}

# Renders the final compose configuration after variable interpolation.
function Show-Config {
    Ensure-DockerArtifacts
    Set-ComposeEnvironment
    Invoke-Compose @("config")
}

# Refreshes the cloned repository without starting or stopping containers.
function Update-Repository {
    Ensure-Repository
}

switch ($Action) {
    "up" {
        Start-Stack
    }
    "down" {
        Stop-Stack
    }
    "logs" {
        Show-Logs
    }
    "ps" {
        Show-Status
    }
    "config" {
        Show-Config
    }
    "pull" {
        Update-Repository
    }
}
