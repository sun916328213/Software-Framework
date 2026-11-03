[CmdletBinding()]
param([switch]$Full, [string]$ArtifactsPath)
$ErrorActionPreference = 'Stop'
# Resolve relative paths before changing directory; opt-in isolation never stops a running host.
if ($ArtifactsPath) { $ArtifactsPath = [IO.Path]::GetFullPath($ArtifactsPath) }
function Invoke-Dotnet([string[]]$Arguments) {
    if ($ArtifactsPath) { $Arguments += @('--artifacts-path', $ArtifactsPath) }
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($Arguments -join ' ')" }
}
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    Invoke-Dotnet -Arguments @('build', 'Vision.Workbench/Vision.Workbench.csproj')
    Invoke-Dotnet -Arguments @('test', 'Tests/Vision.Tests/Vision.Tests.csproj', '--filter', 'FullyQualifiedName~FindLineCaliperContractTests|FullyQualifiedName~LineSearchGeometryTests|FullyQualifiedName~FindLineSearchBandTests|FullyQualifiedName~BlobFeatureReportingTests|FullyQualifiedName~P4ParameterMetadataTests|FullyQualifiedName~SearchRegionTests|FullyQualifiedName~BlobSearchRegionTests|FullyQualifiedName~BlobRoiImageTests')
    Invoke-Dotnet -Arguments @('test', 'Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj', '--filter', 'FullyQualifiedName~ControlsNamingTests|FullyQualifiedName~VisionToolEditorCompositionTests|FullyQualifiedName~BlobResultFeatureColumnsTests|FullyQualifiedName~EditorFeedbackTests|FullyQualifiedName~AppearanceEntryTests|FullyQualifiedName~PortableProjectTests|FullyQualifiedName~BrowserVisualStyleTests|FullyQualifiedName~BrowserCompositionTests|FullyQualifiedName~ProjectImportIsolationTests|FullyQualifiedName~VisionProjectFileStoreTests|FullyQualifiedName~RoiDrawingCompletionTests|FullyQualifiedName~BrowserActionLayoutTests|FullyQualifiedName~WorkbenchProjectTests|FullyQualifiedName~VisionPropertyPanelTests|FullyQualifiedName~PropertyPanelCollectionTests|FullyQualifiedName~EditorInteractionSimplificationTests|FullyQualifiedName~BlobBrowserSearchRegionTests|FullyQualifiedName~DetectionResultSelectionTests|FullyQualifiedName~TemplateResultSelectionTests|FullyQualifiedName~RoiImageViewTests|FullyQualifiedName~NestedPropertyPanelTests|FullyQualifiedName~LegacyEntryConvergenceTests|FullyQualifiedName~RoiGeometryCompletionTests|FullyQualifiedName~EditorRefinementTests|FullyQualifiedName~LineRoiUnificationTests|FullyQualifiedName~LineSearchEditorTests|FullyQualifiedName~BrowserLayoutTests|FullyQualifiedName~BrowserInteractionTests|FullyQualifiedName~ResultSelectionInteractionTests|FullyQualifiedName~BlobParametersInteractionTests|FullyQualifiedName~BlobViewportRegressionTests|FullyQualifiedName~P3EditorInteractionTests|FullyQualifiedName~P4ToolEditorTests')
    if ($Full) {
        Invoke-Dotnet -Arguments @('test', 'Tests/Vision.Tests/Vision.Tests.csproj', '--filter', 'Category!=RequiresRuntime')
        Invoke-Dotnet -Arguments @('test', 'Tests/Vision.Workbench.Tests/Vision.Workbench.Tests.csproj', '--filter', 'Category!=RequiresRuntime')
    }
} finally { Pop-Location }
# Does not enable skipped vendor-runtime tests, install SDKs, or exercise real devices.
