# Multi Agent Path Finding Framework and simulator 

A simulation framework for managing and planning multi-agent tasks in a 2D grid environment. Supports loading maps, robots, and tasks from files and uses a pluggable planning interface for strategy development.

---

## Project Structure

```
FrameWork/
├── Direction.cs            # Enum for representing robot direction
├── IPlanner.cs             # Interface for custom planning logic
├── Map.cs                  # 2D grid environment
├── MapSymbols.cs           # Enum representing map contents
├── Position.cs             # Coordinates and movement logic
├── Robot.cs                # Robot entity
├── RobotId.cs              # Robot identifier
├── RobotMaster.cs          # Loads and manages all robots
├── RobotTask.cs            # Represents a pickup-dropoff task
├── SimulationFrameWork.cs  # Simulation runner and validator
├── SimulationState.cs      # Stores all runtime data
└── TaskMaster.cs           # Loads and manages all tasks
```

---

### 1. **Initialize Simulation**

In simulation, we Create a `SimulationFrameWork` with file paths for:
- The map
- Robot positions
- Task list


### 2. **Start the Planner**

```
simulation.StartPlanner();
```

### 3. **Step Through Simulation**

Each tick advances robot movement based on planner output:

```
simulation.Tick();
```

---

##  File Formats
File formats are the same as in the Robot League Runners 
###  `map.txt`
- Line 1:  `type`  type of the map 
- Line 2:  `height` height 
- Line 3:  `width` width 
- Line 4 - `map`
- Following lines : map


Represents the 2D environment:

- `.` = free space  
- Any other character = obstacle  

---

###  `robots.txt`
Defines robot starting positions:
Agents are stored as one number 
- x = `number`%`height` 
- y = `number`/`height`

File format

- Line 1: version
- Line 2: total number of robots
- Following lines: `number` positions


###  `tasks.txt`
Defines robot tasks (pickup → destination):
Tasks are stored as two numbers

- pickup x = `number1`%`height` 
- pickup y = `number1`/`height`

- drop off x = `number2`%`height` 
- drop off y = `number2`/`height`


File format
- Line 1: version
- Line 2: total number of tasks
- Following lines: `number1,number2`


##  Implementing a Custom Planner

Implement the `IPlanner` interface:

```
public class MyPlanner : IPlanner
{
    public void StartPlanning() { /* your logic */ }
    public bool HasNextMove() => true;
    public Dictionary<RobotId, Direction>? GetNextMove() => ...;
    public bool IsFinished() => false;
    public void Reset() { }
}
```


---

##  Key Concepts

- **SimulationState**: Holds the map, tasks, and robot info.
- **Validation**: Simulation prevents collisions and out-of-bounds moves.
- **Modular Design**: You can swap in new planners without modifying the core logic.

---

##  Map & Robot Dynamics

- Robots can move **Up, Down, Left, Right**
- The planner returns a map of `RobotId → Direction`
- The framework ensures:
  - No two robots share the same target position
  - No robot moves into an obstacle or out of bounds



    

#  Simulation Program for Multi-Robot Framework

This project serves as a visual and execution wrapper around the core Multi Agent Path Finding Framework. It launches a simulation run and visualizes it via an ASCII-based Avalonia UI.

---
##  Folder Structure

```
MAPF_Highway/
├── Config.cs             # File paths and settings
├── ConsoleRenderer.cs    # Avalonia-based ASCII UI
├── SimulationRunner.cs   # Simulation control logic
└── Program.cs            # App entry point
```

---
## Purpose

- Run a simulation of multiple robots executing tasks on a grid map.
- Display real-time updates in a desktop window using ASCII graphics.
- Use a custom planner plugged into the core framework.

---


### 1. **Entry Point**

```
// Program.cs
static void Main(string[] args)
```

- Starts the Avalonia UI in the background
- Launches the simulation runner asynchronously
- Keeps console alive until user exits

---

### 2. **Configuration**

Defined in `Config.cs`:

```csharp
public static class Config
{
    public static readonly string MapName;
    public static readonly string RobotName;
    public static readonly string TaskName;
    public const int Steps = 10;
}
```

Update file paths and step count to control simulation input.

---

### 3. **Simulation Execution**

`SimulationRunner.cs` coordinates each step:

- Initializes the framework and planner
- Each second:
  - Advances simulation (`Tick`)
  - Collects map, robots, tasks
  - Renders them using `ConsoleRenderer`

---

### 4. **Rendering**

Powered by **Avalonia UI** (`ConsoleRenderer.cs`):

- A `TextBlock` updates with a fresh ASCII map each tick
- Symbols are inserted dynamically:
  - `.` = free space
  - `#` = obstacle
  - `R` = robot
  - `P` = pickup
  - `D` = destination