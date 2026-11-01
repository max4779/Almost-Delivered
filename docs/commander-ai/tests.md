# Reproduce automated verification

Use the existing Unity 2021.3.45f2 editor and Unity Test Framework 1.1.33. No new packages are required. From the project root in PowerShell:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe'
$project = (Get-Location).Path
New-Item -ItemType Directory -Force TestResults, Logs | Out-Null

# Run each command separately and wait for its Unity process to exit.
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testResults "$project\TestResults\editmode.xml" -logFile "$project\Logs\editmode.log"
& $unity -batchmode -nographics -projectPath $project -runTests -testPlatform PlayMode -testResults "$project\TestResults\playmode.xml" -logFile "$project\Logs\playmode.log"
```

On Windows, use `Start-Process -PassThru -WindowStyle Hidden -Wait` when the shell returns before Unity exits, and inspect that process's `ExitCode` plus the XML. The required result is Unity exit 0, zero failed/skipped tests, and saved NUnit XML. Omit `-quit`: this installed test-runner version owns the test process lifetime. Do not run two editors against the same project directory.

For focused verification, append one of these filters to a PlayMode command:

```text
-testFilter AlmostDelivered.Tests.PlayMode.CommanderAIStressTests
-testFilter AlmostDelivered.Tests.PlayMode.CommanderScenarioTests
-testFilter AlmostDelivered.Tests.PlayMode.AlertFlowTests
-testFilter AlmostDelivered.Tests.PlayMode.CommanderLifecycleTests
```

The EditMode filter `AlmostDelivered.Tests.EditMode.CommanderBrainTests` includes configurable command-cycle decay and immediate-refresh regressions. The stress fixture uses real perception, authored tactics/playbooks, allocation, baked NavMesh, and member executors. It asserts finite positions and scores, capacity-safe reservations, bounded/exact cycle counts, real movement, and CPU sample availability. The Unity runner fails unexpected error/exception logs. No test imposes hardware-specific millisecond limits.

Regenerate the arena with:

```powershell
& $unity -batchmode -nographics -projectPath $project -executeMethod AlmostDelivered.Editor.CommanderAI.CommanderAITestSceneBuilder.BuildFromCommandLine -logFile "$project\Logs\arena-build.log"
```

The entrypoint exits explicitly. Inspect `COMMANDER_ARENA_BUILT`, exit 0, and Git changes. Scene object file IDs can be regenerated, so byte-identical scene YAML is not promised: compare stable names, wiring, actor counts, configurations, GUIDs, and navigation data, as the builder tests do. Keep only intended generated changes.

If a user-owned editor locks this project, leave it running. Use a fresh temporary project mirror with Assets, Packages, and ProjectSettings; verify source SHA-256 hashes before launch, and retain the XML/log provenance. Never close a user's editor to run these commands. No mirror was needed for the recorded Task 8 baseline.

See [profiling](profiling.md) for the recorded results and [portfolio demo](portfolio-demo.md) for the remaining human presentation check.
