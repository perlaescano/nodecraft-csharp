# NodeCraft C# Studio

A browser-based C# programming course, standalone C# scratchpad, and 2D game engine studio.

## Features
- **Two Learning Modes**:
  - **Lessons & Game Engine Mode**: 15 guided lessons covering C# fundamentals and 2D game development (`Node`, `Node2D`, `Sprite2D`, `Label`, `Vector2`, `Input`, `Audio`, collisions, and platformer physics).
  - **C# Practice Scratchpad Mode**: Practice pure C# with clean `Program.Main()` or top-level `Console.WriteLine()` starter templates, one-click C# building blocks, and a dedicated terminal output panel.
- **Built-in Pixel-Art & Sound Asset Library (`🎨 Asset Library`)**: Includes 10 ready-to-use pixel sprites (`knight`, `slime`, `coin`, `gem`, `spaceship`, `laser`, `star`, `heart`, `tree`, `crate`) and 6 synthesized retro arcade sound effects (`coin`, `jump`, `laser`, `explosion`, `powerup`, `hit`) with one-click code insertion.
- **VS Code Dark+ Color-Coded Editor**: Syntax highlighting, line numbers, auto-indentation, and `Ctrl+Enter` execution.
- **100% Client-Side WebAssembly Execution**: Zero external assets or servers—completely safe and unblocked on school Chromebooks.

## Run Locally
1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Open a terminal inside this folder and run:
   ```bash
   dotnet run
   ```
3. Open the `http://localhost:xxxx` link shown in the terminal.

## Educational & Open-Source Disclaimer
NodeCraft C# Studio is a non-commercial, not-for-profit educational platform created solely to teach C# programming and 2D game development concepts to students.

**Independent Project Notice:** Its node-and-scene architecture is inspired by Godot Engine (licensed under the MIT License) so students can transfer skills to industry tools. NodeCraft C# Studio is an independent project and is **not** affiliated with, endorsed by, or sponsored by the Godot Foundation or the Godot Engine project. "GODOT" is a trademark of the Godot Foundation.

**Student Privacy & Sandbox Notice:** All C# code compilation and execution occur locally inside the user's web browser via WebAssembly. No student code, personal information, or account data is transmitted to or stored on external servers.
