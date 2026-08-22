在 VS 2022 开发环境中构建 Release 版：

```powershell
Set-Location E:\OpenChamber\bun-v1.3.14-shim-hardlink
& 'C:\Users\luosh\.config\opencode\skills\visual-studio-build\scripts\Enter-VisualStudioDevShell.ps1' `
    -Version 2022 -Architecture x64
$env:PATH = 'E:\OpenChamber\toolchains\LLVM-21.1.8\bin;C:\Program Files\Git\usr\bin;' + $env:PATH
$env:BUN_DEBUG_QUIET_LOGS = '1'
& 'C:\Users\luosh\.bun\bin\bun.exe' scripts\build.ts `
    --profile=release `
    --canary=off `
    '--cache-dir=E:\OpenChamber\bun-build-cache-release' `
    --quiet
```

产物位于：

```text
E:\OpenChamber\bun-v1.3.14-shim-hardlink\build\release\bun.exe
```

验证：

```powershell
.\build\release\bun.exe --revision
```
