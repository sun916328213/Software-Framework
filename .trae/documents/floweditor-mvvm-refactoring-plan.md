# FlowEditor MVVM Architecture Refactoring Plan

## Context

The FlowEditor is a WPF Class Library (.NET 9.0) for visual flowchart editing, already using CommunityToolkit.Mvvm 8.4.2, Prism 8.1.97, and MaterialDesignThemes 5.3.0. The current architecture has fundamental MVVM violations:

- **FlowChartViewModel** is 794 lines of mixed UI state + business logic (connection validation, clipboard, auto-bind, cycle detection)
- **FlowChartView.xaml.cs** is 504 lines with hit testing, context menu building, and visual tree traversal
- **NodeView.xaml.cs** is 199 lines of drag logic and visual tree traversal
- **FlowChartContainerView.xaml.cs** has drag-drop and export logic in code-behind
- **No service interfaces** exist — making DI, testing, and loose coupling impossible
- **FlowChartService** is a mixed bag of pass-through and business logic (451 lines)
- **Toolbox initialization is duplicated** between FlowChartViewModel and FlowChartContainerViewModel

### Goal

Refactor to a strict 4-layer MVVM architecture (View → ViewModel → Service → Model) with:
- All business logic in dedicated services behind interfaces
- ViewModels under 250 lines, containing only UI state and command delegation
- Code-behind files under 100 lines, containing only rendering code
- Full Prism DI wiring with proper constructor injection
- CommunityToolkit.Mvvm source generators for `[ObservableProperty]` and `[RelayCommand]`

---

## Target Architecture

```
View (XAML + minimal code-behind)
  └─ Binds to ViewModel properties/commands
  └─ Contains ONLY rendering code that can't be expressed in XAML

ViewModel (ObservableObject + [RelayCommand])
  └─ Contains ONLY UI state + command delegation to services
  └─ All dependencies via constructor injection

Service (interface + implementation)
  └─ Contains ALL business logic, orchestration, validation
  └─ Single responsibility per service
  └─ Registered via Prism DI

Model (ObservableObject data objects)
  └─ Pure data with property change notification
  └─ No dependency on ViewModels or Services
```

## Target Directory Structure

```
FlowEditor/
  Interfaces/                     (NEW)
    IFlowChartService.cs
    IExportService.cs
    IUndoRedoService.cs
    IPersistenceService.cs
    IToolDiscoveryService.cs
    IExecutionService.cs
    IConnectionService.cs
    IClipboardService.cs
    ISelectionService.cs
    IZoomService.cs
    IToolboxService.cs
  Services/                       (EXPANDED)
    FlowChartService.cs           (refactored: ~200 lines)
    ExecutionService.cs           (NEW: extracted from FlowChartService)
    ConnectionService.cs          (NEW: extracted from FlowChartViewModel)
    ClipboardService.cs           (NEW: extracted from FlowChartViewModel)
    SelectionService.cs           (NEW: extracted from FlowChartViewModel)
    ZoomService.cs                (NEW: extracted from FlowChartService)
    ToolboxService.cs             (NEW: de-duplicated)
    PersistenceService.cs         (NEW: wraps multi-flow persistence)
    FlowChartPersistenceService.cs (moved from Core/)
    FlowToolDiscoveryService.cs   (moved from Core/)
    ExportService.cs              (minor: add interface)
    UndoRedoService.cs            (minor: add interface)
  Core/                           (KEPT)
    FlowChartExecutionEngine.cs   (unchanged)
    FlowToolAttribute.cs          (unchanged)
  ViewModels/
    FlowChartViewModel.cs         (SLIMMED: ~200 lines)
    FlowChartContainerViewModel.cs (SLIMMED: ~200 lines)
    NodeViewModel.cs              (unchanged)
  Views/
    FlowChartView.xaml.cs         (SLIMMED: ~100 lines)
    FlowChartContainerView.xaml.cs (SLIMMED: ~50 lines)
    NodeView.xaml.cs              (SLIMMED: ~50 lines)
  Helpers/                        (NEW: shared utilities)
    CanvasVisualHelper.cs
    PortPositionHelper.cs
    ContextMenuFactory.cs
  Models/                         (UNCHANGED)
    + ToolboxModels.cs            (NEW: NodePaletteItem, etc.)
  Nodes/                          (UNCHANGED)
  Converters/                     (UNCHANGED)
  Resources/                      (UNCHANGED)
```

---

## Implementation Steps

### Phase 1: Create Service Interfaces (Low Risk, Additive)

**Files to create (11 new):**
- `Interfaces/IFlowChartService.cs` — Model, Nodes, Connections, UndoRedo, AddNode, FindNode, ClearAll, CurrentFilePath, AppendLog
- `Interfaces/IExportService.cs` — ExportToPngWithDialog, ExportToSvgWithDialog
- `Interfaces/IUndoRedoService.cs` — CanUndo, CanRedo, Push, Undo, Redo, Clear
- `Interfaces/IPersistenceService.cs` — Save, Load, BuildDocument, RebuildNode
- `Interfaces/IToolDiscoveryService.cs` — Discover, DiscoveredTools
- `Interfaces/IExecutionService.cs` — RunAsync, RunLoopAsync, Stop, StopLoop, StepContinue
- `Interfaces/IConnectionService.cs` — StartConnection, CompleteConnection, CancelConnection, WouldCreateCycle, AutoBindDataPorts, DeleteConnection
- `Interfaces/IClipboardService.cs` — CopySelected, PasteAtCenter
- `Interfaces/ISelectionService.cs` — SelectedNode, SelectedConnection, SelectedNodes, ClearSelection
- `Interfaces/IZoomService.cs` — ZoomIn, ZoomOut, ZoomReset, ZoomToFit, ZoomToFitRequested event
- `Interfaces/IToolboxService.cs` — NodePalette, ToolboxCategories, ToolboxSearchText, Initialize, ExcludeToolByName

**Files to modify:**
- `FlowChartService.cs` — add `: IFlowChartService`
- `ExportService.cs` — add `: IExportService`
- `UndoRedoService.cs` — add `: IUndoRedoService`
- `FlowChartPersistenceService.cs` — add `: IPersistenceService`
- `FlowToolDiscoveryService.cs` — add `: IToolDiscoveryService`

### Phase 2: Create Shared Helpers (Low Risk)

**Files to create:**
- `Helpers/CanvasVisualHelper.cs` — extract FindAncestorNodeView, FindParentCanvas, FindFlowChartViewModel, FindVisualChild from View code-behind
- `Helpers/PortPositionHelper.cs` — extract FindPortCenterOnCanvas, GetPortCenter, FindPortEllipse, FindAncestorEllipseWithName
- `Helpers/ContextMenuFactory.cs` — extract ShowNodeContextMenu, ShowCanvasContextMenu

**Files to modify:**
- `FlowChartView.xaml.cs` — replace extracted methods with helper calls
- `NodeView.xaml.cs` — replace extracted methods with helper calls
- `FlowChartContainerView.xaml.cs` — replace FindVisualChild with helper

### Phase 3: Create New Service Classes (Low Risk, Additive)

**Files to create:**
- `Services/ExecutionService.cs` — extract RunAsync, RunLoopAsync, Stop, StopLoop, StepContinue, ValidateINConnections from FlowChartService
- `Services/ConnectionService.cs` — extract StartConnection, CompleteConnection, CancelConnection, WouldCreateCycle, AutoBindDataPorts, DeleteConnection from FlowChartViewModel
- `Services/ClipboardService.cs` — extract CopySelected, PasteAtCenter, RebuildNodeFromClipboard from FlowChartViewModel
- `Services/SelectionService.cs` — extract SelectedNode, SelectedConnection, SelectedNodes management from FlowChartViewModel
- `Services/ZoomService.cs` — extract ZoomIn, ZoomOut, ZoomReset, ZoomToFit from FlowChartService
- `Services/ToolboxService.cs` — de-duplicate toolbox initialization from both ViewModels
- `Services/PersistenceService.cs` — extract multi-flow SaveAll/LoadAll from FlowChartContainerViewModel

### Phase 4: Slim Down ViewModels (Medium Risk)

**Files to modify:**
- `FlowChartViewModel.cs` (794→~200 lines):
  - Accept all service interfaces via constructor injection
  - Remove parameterless constructor
  - Remove connection validation, clipboard, auto-bind, cycle detection, toolbox init, zoom logic
  - Commands become thin pass-throughs: `private void CopySelected() => _clipboardService.CopySelected(SelectedNode);`
  - Use `[ObservableProperty]` and `[RelayCommand]` source generators for remaining properties/commands
  - Move NodePaletteItem, ToolboxCategory, ToolboxSubGroup to `Models/ToolboxModels.cs`

- `FlowChartContainerViewModel.cs` (421→~200 lines):
  - Accept IToolboxService, IPersistenceService, IContainerProvider via DI
  - Remove toolbox init, multi-flow persistence, BuildToolboxCategories, FilterToolbox
  - Expose toolbox as pass-through from IToolboxService

- `Models/ToolboxModels.cs` (NEW) — move NodePaletteItem, ToolboxCategory, ToolboxSubGroup here

### Phase 5: Refactor Existing Services (Medium Risk)

**Files to modify:**
- `FlowChartService.cs` (451→~200 lines):
  - Remove execution logic, zoom logic, ValidateINConnections
  - Keep Model ownership, Nodes/Connections collections, UndoRedo, AddNode, FindNode, ClearAll, AppendLog
  - Remove parameterless constructor, require DI

**Files to move:**
- `Core/FlowChartPersistenceService.cs` → `Services/FlowChartPersistenceService.cs` (update namespace + all usings)
- `Core/FlowToolDiscoveryService.cs` → `Services/FlowToolDiscoveryService.cs` (update namespace + all usings)

### Phase 6: Update Prism DI Wiring (Medium Risk)

**File to modify:**
- `FlowEditorModule.cs` — register all new services (singletons: IToolDiscoveryService, IToolboxService, IUndoRedoService, IExportService, FlowChartExecutionEngine; transient: IFlowChartService, IExecutionService, IConnectionService, IClipboardService, ISelectionService, IZoomService, IPersistenceService)

### Phase 7: Extract Remaining Code-Behind (Low Risk)

**Files to modify:**
- `FlowChartView.xaml.cs` (504→~100 lines) — replace all visual tree traversal with helpers, replace context menu building with ContextMenuFactory, move zoom-to-fit arithmetic to ZoomService
- `NodeView.xaml.cs` (199→~50 lines) — replace all visual tree traversal with helpers
- `FlowChartContainerView.xaml.cs` (125→~50 lines) — replace FindVisualChild with CanvasVisualHelper

### Phase 8: Performance Optimizations (Low Risk)

**Files to modify:**
- `FlowChartViewModel.cs` — use `[RelayCommand]` source generators, eliminate manual `InitializeCommands()` and `NotifyCanExecuteChanged()`
- `FlowChartContainerViewModel.cs` — use `[RelayCommand]` source generators
- `FlowChartViewModel.cs` — use `[ObservableProperty]` source generators for remaining manual properties

---

## Key Design Decisions

1. **Keep `FlowChartService.Model` property** — it's used extensively in XAML bindings (`{Binding Model.ZoomLevel}`, etc.)
2. **Keep `FlowChartCanvas` named element** — CanvasVisualHelper uses it for lookups
3. **Defer removing `NodeModel.Service`** — it's used by the external `WPFCustControl` assembly's `IDataBindingProvider`. Document as architectural debt.
4. **NodePaletteItem constructor** — change from taking `IContainerProvider` to `Func<NodeModel>` to avoid DI container leak
5. **Keep public API signatures** — all public properties/methods on FlowChartViewModel, FlowChartService, etc. maintain same signatures

## Verification

1. **Build**: `dotnet build` the entire solution, verify zero errors
2. **Runtime**: Open the EditorDemo application, verify:
   - Toolbox loads with all node types
   - Nodes can be added, selected, dragged, deleted
   - Connections can be created, validated, deleted
   - Run/Stop/Loop execution works
   - Save/Load single and multi-flow files
   - Undo/Redo works
   - Zoom works (Ctrl+scroll, buttons, fit)
   - Export PNG works
   - Context menus work (node and canvas)
   - Tab management works (add, close, rename)
   - ToolBlock internal editor works
3. **Code review**: Verify all ViewModels under 250 lines, all code-behind under 100 lines, all services under 300 lines

## Critical Files

- `FlowEditorModule.cs` — DI registration hub
- `ViewModels/FlowChartViewModel.cs` — primary refactoring target (794 lines)
- `Services/FlowChartService.cs` — secondary refactoring target (451 lines)
- `Views/FlowChartView.xaml.cs` — code-behind extraction target (504 lines)
- `Views/NodeView.xaml.cs` — code-behind extraction target (199 lines)
- `Views/FlowChartContainerView.xaml.cs` — code-behind extraction target (125 lines)
- `ViewModels/FlowChartContainerViewModel.cs` — toolbox de-duplication target (421 lines)