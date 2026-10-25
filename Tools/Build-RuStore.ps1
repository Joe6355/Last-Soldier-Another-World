param(
    [ValidateSet('QA','Release')] [string]$Mode = 'QA',
    [switch]$SkipExport
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolRoot = Join-Path $projectRoot 'Builds\Toolchain'
$unity = Join-Path $toolRoot 'Unity2022.3.62f2\Editor\Unity.exe'
$java = Get-ChildItem -LiteralPath $toolRoot -Directory | Where-Object Name -Like 'jdk-17*' | Select-Object -First 1
$gradle = Join-Path $toolRoot 'gradle-8.9\bin\gradle.bat'
if (-not (Test-Path -LiteralPath $unity) -or -not $java -or -not (Test-Path -LiteralPath $gradle)) {
    throw 'Install the isolated Unity 2022.3.62f2, JDK 17 and Gradle 8.9 under Builds/Toolchain first.'
}
$output = Join-Path $projectRoot 'Builds\RuStore'
$logRoot = Join-Path $projectRoot 'Builds\RuStoreValidation'
New-Item -ItemType Directory -Path $output,$logRoot -Force | Out-Null
function Invoke-UnityStep([string]$Method,[string]$Log) {
    $process = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$projectRoot+'"'),
        '-buildTarget','Android','-executeMethod',$Method,'-job-worker-count','4','-logFile',('"'+$Log+'"')) -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity failed ($($process.ExitCode)); inspect $Log" }
}
if (-not $SkipExport) {
    $suffix = if ($Mode -eq 'QA') { 'Qa' } else { 'Release' }
    Invoke-UnityStep "YandexBuild.PrepareRuStore$suffix" (Join-Path $logRoot "prepare-$Mode.log")
    Invoke-UnityStep "YandexBuild.BuildRuStore$suffix" (Join-Path $logRoot "export-$Mode.log")
}
$export = Join-Path $output $(if ($Mode -eq 'QA') { 'GradleQA' } else { 'GradleRelease' })
if (-not (Test-Path -LiteralPath (Join-Path $export 'settings.gradle'))) { throw 'Export the matching Unity configuration first.' }
$settings = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets\Resources\RuStoreSettings.asset') -Raw
if ($Mode -eq 'Release') {
    if ($settings -match 'useTestAds:[ \t]*1' -or $settings -notmatch 'releasePrivacyReviewed:[ \t]*1' -or $settings -match '(?m)^  (rewardedAdUnitId|interstitialAdUnitId|bannerAdUnitId):[ \t]*\r?$') {
        throw 'Release services are not configured. QA cannot be published as production.'
    }
}
$properties = Join-Path $export 'gradle.properties'
$propertyLines = Get-Content -LiteralPath $properties | Where-Object { $_ -notmatch '^android\.aapt2FromMavenOverride=' }
Set-Content -LiteralPath $properties -Value $propertyLines -Encoding UTF8
$previousJava = $env:JAVA_HOME
$previousGradle = $env:GRADLE_USER_HOME
try {
    $env:JAVA_HOME = $java.FullName
    $env:GRADLE_USER_HOME = Join-Path $toolRoot 'gradle-cache'
    & $gradle --no-daemon --max-workers 4 --console plain -p $export :launcher:assembleRelease 2>&1 |
        Tee-Object -FilePath (Join-Path $logRoot "gradle-$Mode.log")
    if ($LASTEXITCODE -ne 0) { throw "Gradle failed with exit code $LASTEXITCODE" }
    $apk = Join-Path $export 'launcher\build\outputs\apk\release\launcher-release.apk'
    if (-not (Test-Path -LiteralPath $apk)) { throw 'Gradle did not produce a signed release APK.' }
    $destination = Join-Path $output $(if ($Mode -eq 'QA') { 'last-soldier-rustore-qa.apk' } else { 'last-soldier-rustore.apk' })
    Copy-Item -LiteralPath $apk -Destination $destination -Force
    Get-Item -LiteralPath $destination | Select-Object FullName,Length
    Get-FileHash -LiteralPath $destination -Algorithm SHA256
} finally {
    $env:JAVA_HOME = $previousJava
    $env:GRADLE_USER_HOME = $previousGradle
}
