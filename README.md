# MAPF Highway

Simulation and planning stack for multi-agent path finding (MAPF) experiments: grid maps and tasks, batch planners (including waypoint-based routing and SAT-backed single-agent legs), and an optional Avalonia-based viewer.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Optional: native **MAPF SAT bridge** for planners that call `libmapf_sat_bridge` (see below)

## Build and test

```bash
dotnet build "MAPF Highway.sln"
dotnet test Test/Test.csproj
```

Run the simulator with your working directory set so `FrameWork/Data/...` resolves (see `Simulator/Config.cs` defaults), or adjust `Config.DataRoot` / file paths.

## Third-party: MAPF-encodings (SAT encodings for MAPF)

**`MAPF-encodings/` is not part of this Git repository.** It is a **separate project** (Jiří Švancára’s **“SAT encodings for MAPF”** work; see the banner in `MAPF-encodings/src/main.cpp`: *Created by Jiri Svancara @ MFF UK*). **We only use it** as a native dependency: the .NET code loads `libmapf_sat_bridge` and calls `mapf_sat_solve_leg` via `WaypointSatLegPathfinder`.

### Pull upstream and place it so the simulator finds the bridge

1. **Clone** the upstream repo **into your working copy**, next to `FrameWork/`, `Planner/`, and `MAPF Highway.sln`. The folder **must** be named `MAPF-encodings` so the default locator can find it:

   ```bash
   cd /path/to/MAPF Highway
   git clone https://github.com/svancaj/MAPF-encodings.git MAPF-encodings
   ```

2. **Build** the static library and the **shared bridge** (needs `make`, `g++`, and dependencies described in `MAPF-encodings/README.md`):

   ```bash
   cd MAPF-encodings
   make lib
   make bridge
   ```

   This produces `MAPF-encodings/release/libmapf_sat_bridge.dylib` (macOS) or `libmapf_sat_bridge.so` (Linux). On **macOS**, if the default build fails on bundled libs, run `bash build_native_libs_macos.sh` from `MAPF-encodings/` first (it builds CaDiCaL/PB and then you can run `make lib` / `make bridge` as needed—see comments in that script).

3. **Discovery:** `MapfSatBridgeLocator` walks up from the process **current directory** and from the app **base directory** looking for `MAPF-encodings/release/<native library>`. Keeping the clone **beside** the solution (layout below) is enough if you run the simulator from the repo root or from `Simulator/bin/...`.

   ```text
   MAPF Highway/
     MAPF Highway.sln
     FrameWork/
     Planner/
     MAPF-encodings/
       release/
         libmapf_sat_bridge.dylib   # or .so / .dll after build
   ```

### Native SAT bridge overrides (optional)

If the library lives elsewhere, set either:

- **`MAPF_SAT_BRIDGE`** to the full path of the shared library, or  
- **`Config.MapfSatBridgeLibraryPath`** in the simulator.

Without one of these and without `MAPF-encodings/release` as above, planners that need SAT will report the bridge as missing.

## Repository layout (high level)

| Path | Role |
|------|------|
| `FrameWork/` | Map, tasks, robots, simulation state |
| `Planner/` | Planners, waypoint graph, SAT leg pathfinder |
| `Simulator/` | Entry point, config, UI |
| `WaypointTool/` | CLI to generate waypoint JSON |
| `Test/` | Unit and integration tests |
| `scripts/` | Optional Python helpers (see `scripts/requirements.txt`) |
