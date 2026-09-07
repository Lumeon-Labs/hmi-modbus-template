# Architecture Overview

## Layer Structure

```
┌───────────────────────────────────────────────┐
│                 HmiTemplate.UI                 │
│  Views  ←→  ViewModels  ←→  (DI Container)    │
└───────────────────────┬───────────────────────┘
                        │ ProjectReference
┌───────────────────────▼───────────────────────┐
│               HmiTemplate.Core                 │
│  Services  ←→  Models  ←→  Data               │
└───────────────────────────────────────────────┘
```

## Service Dependency Graph

```
App.xaml.cs
  ├── ConfigStore          (load config.json)
  ├── ModbusSlaveService   (TCP Listener :502)
  │     └── TcpListener + IModbusSlave
  ├── DataSimulatorService (writes to Slave registers @ 500ms)
  │     └── depends on ModbusSlaveService
  ├── ModbusMasterService  (TCP Client → Slave)
  │     └── emits DataReceived event
  ├── AlarmEngine          (consumes DataReceived)
  │     └── AlarmHistory
  └── RecipeManager        (writes HR4/HR5/HR6 via Master)
```

## Data Flow

```
DataSimulatorService
  → writes HR0~HR3, Coil0~Coil3
  → ModbusSlaveService (TCP Listener)

ModbusMasterService
  ← reads HR0~HR6, Coil0~Coil3 every 500ms
  → DataReceived event

Subscribers of DataReceived:
  1. DashboardViewModel  → update UI bindings
  2. TrendViewModel      → add to LiveCharts series
  3. AlarmEngine.Update  → evaluate alarm rules
```

## MVVM Pattern

- **Model**: `ProcessData`, `AlarmRecord`, `RecipeModel`
- **ViewModel**: `CommunityToolkit.Mvvm` with `[ObservableProperty]` + `[RelayCommand]`
- **View**: Pure XAML, binds to ViewModel properties
- **Navigation**: `MainViewModel.CurrentView` + `DataTemplate` in `MainWindow.xaml`

## Key Design Decisions

1. **Built-in Slave**: No external PLC/simulator needed — the app hosts its own Modbus TCP server on Port 502.
2. **Auto-reconnect**: Master polls every 500ms. On failure → 5s backoff → retry indefinitely.
3. **Hysteresis alarms**: Trigger at threshold, clear only below threshold (prevents chattering at boundary values).
4. **Thread safety**: `TrendBuffer` and `AlarmHistory` use `lock`. UI updates via `Dispatcher.Invoke`.
