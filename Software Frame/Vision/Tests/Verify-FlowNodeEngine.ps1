# 离线节点模型验证；先完整构建生产项目，再直接编译现有Workbench测试线中的指定测试。
# 不启动Application、窗口、Workbench、MachineApp、授权程序或HALCON；不新增/裁剪生产项目。
param([string]$Label = 'green')
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$artifacts = Join-Path $root 'Software Frame/Vision/Tests/obj/editor-verification'
$output = Join-Path $artifacts 'flow-node-engine-tests'
foreach ($project in 'Vision/Vision.FlowNode/Vision.FlowNode.csproj', 'Vision/Tools/OpenCv/Vision.Tools.OpenCv.csproj') {
    & dotnet build (Join-Path $root "Software Frame/$project") --no-restore --artifacts-path $artifacts -m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false -nr:false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$cached = Join-Path $artifacts 'bin/Vision.Tests/debug'
if (!(Test-Path (Join-Path $cached 'xunit.core.dll'))) { throw 'Missing existing xUnit/VSTest verification assets; restore existing test dependencies first.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Get-ChildItem $cached -File | Where-Object { $_.Extension -in '.dll', '.json' } | Copy-Item -Destination $output -Force
foreach ($project in 'Vision.FlowNode', 'Vision.Tools.OpenCv') {
    Get-ChildItem (Join-Path $artifacts "bin/$project/debug") -File -Filter '*.dll' | Copy-Item -Destination $output -Force
}
# 根据当前FlowNode资产复制实际解析到的包运行时DLL，并合并当前依赖清单；不借旧UI依赖求绿。
$assets = Get-Content (Join-Path $artifacts 'obj/Vision.FlowNode/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$target = @($assets.targets.Values)[0]
$packageRoot = @($assets.packageFolders.Keys)[0]
foreach ($library in $target.GetEnumerator()) {
    if ($assets.libraries[$library.Key].type -ne 'package') { continue }
    foreach ($runtimeAsset in $library.Value.runtime.Keys) {
        if ($runtimeAsset -like '*.dll') {
            Copy-Item (Join-Path $packageRoot ($assets.libraries[$library.Key].path + '/' + $runtimeAsset)) $output -Force
        }
    }
}
$depsPath = Join-Path $output 'Vision.Tests.deps.json'
$deps = Get-Content $depsPath -Raw | ConvertFrom-Json -AsHashtable
$flowDeps = Get-Content (Join-Path $artifacts 'bin/Vision.FlowNode/debug/Vision.FlowNode.deps.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($item in $flowDeps.libraries.GetEnumerator()) { $deps.libraries[$item.Key] = $item.Value }
$depsTarget = $deps.targets[$deps.runtimeTarget.name]
foreach ($item in $flowDeps.targets[$flowDeps.runtimeTarget.name].GetEnumerator()) { $depsTarget[$item.Key] = $item.Value }
$deps | ConvertTo-Json -Depth 100 | Set-Content $depsPath
$runtime = Join-Path $cached 'runtimes'
if (Test-Path $runtime) { Copy-Item $runtime $output -Recurse -Force }
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$sdk = (& dotnet --version).Trim()
$compiler = Join-Path $dotnetRoot "sdk/$sdk/Roslyn/bincore/csc.dll"
$refsByName = @{}
foreach ($packName in 'Microsoft.NETCore.App.Ref', 'Microsoft.WindowsDesktop.App.Ref') {
    $pack = Get-ChildItem (Join-Path $dotnetRoot "packs/$packName") -Directory |
        Where-Object { $_.Name -like '9.*' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    Get-ChildItem (Join-Path $pack.FullName 'ref/net9.0') -Filter '*.dll' | ForEach-Object { $refsByName[$_.Name] = $_.FullName }
}
$references = @($refsByName.Values | ForEach-Object { '/reference:' + $_ })
foreach ($dependency in 'Vision.Core', 'Vision.FlowNode', 'Vision.Tools', 'FlowEditor.Core', 'FlowEditor.Controls', 'FlowEditor.Runtime', 'WPFCustomControls.PropertyGrid.Core', 'CommunityToolkit.Mvvm', 'xunit.core', 'xunit.assert', 'xunit.abstractions') {
    $references += '/reference:' + (Join-Path $output "$dependency.dll")
}
$globals = Join-Path $artifacts 'obj/Vision.Tests/debug/Vision.Tests.GlobalUsings.g.cs'
$source = Join-Path $root 'Software Frame/Vision/Tests/Vision.Workbench.Tests/Controls/EngineVisionNodeTests.cs'
$framework = Join-Path $output 'Framework.cs'
'[assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v9.0")]' | Set-Content $framework
& dotnet $compiler /nologo /target:library /langversion:13 /nullable:enable "/out:$(Join-Path $output 'Vision.Tests.dll')" @references $source $globals $framework
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet vstest (Join-Path $output 'Vision.Tests.dll') "/TestAdapterPath:$output" '/TestCaseFilter:FullyQualifiedName~EngineVisionNodeTests' '/Framework:.NETCoreApp,Version=v9.0' "/Logger:trx;LogFileName=flow-node-engine-$Label.trx" "/ResultsDirectory:$(Join-Path $output 'results')"
exit $LASTEXITCODE
