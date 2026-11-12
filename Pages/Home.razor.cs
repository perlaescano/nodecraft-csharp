using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NodeCraftCSharp.Engine;

namespace NodeCraftCSharp.Pages;

public partial class Home : IAsyncDisposable
{
    private record Lesson(string Track, string Title, string Concepts, string Instructions, string Challenge, string StarterCode);
    private record ScratchpadTemplate(string Title, string Description, string StarterCode);
    private record SpriteAsset(string Id, string Label, string Snippet);
    private record SoundAsset(string Id, string Label, string Icon, string Snippet);

    private readonly InBrowserCSharpCompiler _compiler = new();
    private readonly SceneTree _tree = new();
    private readonly FrameRenderContext _renderContext = new();
    private readonly List<string> _consoleLogs = new();
    private DotNetObjectReference<Home>? _dotNetRef;
    private CancellationTokenSource? _loopCts;

    private bool _isScratchpadMode = false;
    private string _trackFilter = "all";
    private int _selectedLesson = 0;
    private int _selectedScratchpad = 0;
    private string _code = "";
    private List<string> _errors = new();
    private bool _isReady = false;
    private bool _showDisclaimer = false;
    private bool _showAssetLibrary = false;
    private string _assetTab = "sprites";

    private bool _needsHighlightSync = false;
    private bool _needsCanvasReinit = false;
    private bool _needsSpritePreviewRender = false;

    private sealed class ConsoleRedirector : TextWriter
    {
        private readonly Action<string> _onLine;
        private readonly StringBuilder _buffer = new();

        public ConsoleRedirector(Action<string> onLine) => _onLine = onLine;
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                _onLine(_buffer.ToString().TrimEnd('\r'));
                _buffer.Clear();
            }
            else
            {
                _buffer.Append(value);
            }
        }

        public override void WriteLine(string? value) => _onLine(value ?? "");
    }

    private readonly List<SpriteAsset> _builtInSprites = new()
    {
        new("knight", "Hero Knight", "var hero = new Sprite2D { Texture = \"knight\", Position = new Vector2(220, 220), Size = new Vector2(48, 48) };\nAddChild(hero);\n"),
        new("slime", "Forest Slime", "var slime = new Sprite2D { Texture = \"slime\", Position = new Vector2(320, 220), Size = new Vector2(44, 44) };\nAddChild(slime);\n"),
        new("coin", "Gold Coin", "var coin = new Sprite2D { Texture = \"coin\", Position = new Vector2(160, 160), Size = new Vector2(32, 32) };\nAddChild(coin);\n"),
        new("gem", "Crystal Gem", "var gem = new Sprite2D { Texture = \"gem\", Position = new Vector2(280, 140), Size = new Vector2(32, 32) };\nAddChild(gem);\n"),
        new("spaceship", "Starfighter", "var ship = new Sprite2D { Texture = \"spaceship\", Position = new Vector2(225, 410), Size = new Vector2(48, 48) };\nAddChild(ship);\n"),
        new("laser", "Laser Bolt", "var laser = new Sprite2D { Texture = \"laser\", Position = new Vector2(242, 360), Size = new Vector2(14, 28) };\nAddChild(laser);\n"),
        new("star", "Power Star", "var star = new Sprite2D { Texture = \"star\", Position = new Vector2(120, 120), Size = new Vector2(36, 36) };\nAddChild(star);\n"),
        new("heart", "Health Heart", "var heart = new Sprite2D { Texture = \"heart\", Position = new Vector2(20, 45), Size = new Vector2(28, 28) };\nAddChild(heart);\n"),
        new("tree", "Pixel Pine", "var tree = new Sprite2D { Texture = \"tree\", Position = new Vector2(80, 320), Size = new Vector2(56, 64) };\nAddChild(tree);\n"),
        new("crate", "Wooden Crate", "var crate = new Sprite2D { Texture = \"crate\", Position = new Vector2(180, 340), Size = new Vector2(44, 44) };\nAddChild(crate);\n")
    };

    private readonly List<SoundAsset> _builtInSounds = new()
    {
        new("coin", "Coin Pickup", "🪙", "Audio.Play(\"coin\");\n"),
        new("jump", "Hero Jump", "🦘", "Audio.Play(\"jump\");\n"),
        new("laser", "Laser Pew", "🔫", "Audio.Play(\"laser\");\n"),
        new("explosion", "Explosion Boom", "💥", "Audio.Play(\"explosion\");\n"),
        new("powerup", "Power-Up Chime", "⭐", "Audio.Play(\"powerup\");\n"),
        new("hit", "Damage Hit", "🛡️", "Audio.Play(\"hit\");\n")
    };

    private readonly List<ScratchpadTemplate> _scratchpadTemplates = new()
    {
        new(
            "1. Blank C# Print Playground",
            "A clean, minimal C# file. Write any C# code inside Main() and use Console.WriteLine() to print text, numbers, or math results!",
            """
            using System;

            public class Program
            {
                public static void Main()
                {
                    Console.WriteLine("Hello, C# World!");
                    Console.WriteLine($"7 x 8 = {7 * 8}");
                }
            }
            """
        ),
        new(
            "2. Variables & Mad-Libs Story Generator",
            "Practice combining string and int variables with string interpolation ($\"...\") to generate a funny adventure story.",
            """
            using System;

            public class Program
            {
                public static void Main()
                {
                    string heroName = "Captain Pixel";
                    string favoriteFood = "tacos";
                    int dragonsDefeated = 4;

                    Console.WriteLine($"Once upon a time, {heroName} went on a quest.");
                    Console.WriteLine($"After eating {dragonsDefeated} {favoriteFood}, {heroName} saved the kingdom!");
                }
            }
            """
        ),
        new(
            "3. Loops & Multiplication Table",
            "Practice using a 'for' loop to print a complete multiplication table in less than 10 lines of code.",
            """
            using System;

            public class Program
            {
                public static void Main()
                {
                    int number = 7;
                    Console.WriteLine($"=== {number} Times Table ===");

                    for (int i = 1; i <= 12; i++)
                    {
                        Console.WriteLine($"{number} x {i} = {number * i}");
                    }
                }
            }
            """
        ),
        new(
            "4. Lists & LINQ Data Analyzer",
            "Practice working with List<int> and C# LINQ methods (.Average(), .Max(), .Min(), .Where()) on game high scores.",
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class Program
            {
                public static void Main()
                {
                    List<int> scores = new() { 420, 890, 1250, 610, 1500, 980 };

                    Console.WriteLine($"All Scores: {string.Join(", ", scores)}");
                    Console.WriteLine($"Highest Score: {scores.Max()}");
                    Console.WriteLine($"Lowest Score: {scores.Min()}");
                    Console.WriteLine($"Average Score: {scores.Average():F1}");

                    var topScores = scores.Where(s => s >= 900);
                    Console.WriteLine($"Pro-Level Scores (900+): {string.Join(", ", topScores)}");
                }
            }
            """
        )
    };

    private readonly List<Lesson> _lessons = new()
    {
        // =========================================================================
        // PART 1: COMPLETE C# PROGRAMMING LANGUAGE COURSE (LESSONS 1 - 7)
        // =========================================================================
        new(
            "csharp",
            "C# 1: Hello C#, Variables & Data Types",
            "string, int, float, bool, Console.WriteLine()",
            "<p>Welcome to <b>C#</b> (pronounced <i>C-Sharp</i>)! Every C# program stores information in typed <b>variables</b>:</p>" +
            "<ul>" +
            "<li><code>string</code> — Text in quotes, like <code>\"Alex\"</code></li>" +
            "<li><code>int</code> — Whole numbers, like <code>100</code></li>" +
            "<li><code>float</code> — Decimal numbers ending in <code>f</code>, like <code>5.5f</code></li>" +
            "<li><code>bool</code> — <code>true</code> or <code>false</code></li>" +
            "</ul>" +
            "<p>Use <code>Console.WriteLine($\"...\")</code> to print variables to the Terminal below, and <code>Label</code> to draw text on screen!</p>",
            "Create a new <code>int level = 1;</code> variable, change <code>playerName</code> to your own name, and print both to the Console!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class VariablesLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    string playerName = "Hero";
                    int health = 100;
                    float speed = 12.5f;
                    bool isReady = true;

                    Console.WriteLine($"Player: {playerName}");
                    Console.WriteLine($"Health: {health} | Speed: {speed} | Ready: {isReady}");

                    var banner = new Label
                    {
                        Position = new Vector2(40, 220),
                        FontSize = 20,
                        Color = "#4ec9b0",
                        Text = $"Welcome, {playerName}! HP: {health}"
                    };
                    AddChild(banner);
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 2: Math, Operators & Type Casting",
            "+, -, *, /, %, (float), Math.Clamp()",
            "<p>C# is a super-fast calculator. You can use <code>+</code>, <code>-</code>, <code>*</code> (multiply), <code>/</code> (divide), and <code>%</code> (remainder/modulo).</p>" +
            "<p><b>Important C# Rule:</b> Dividing two <code>int</code> numbers drops the decimal (<code>7 / 2 == 3</code>). To keep decimals, use <code>float</code> or <code>double</code> (<code>7.0 / 2.0 == 3.5</code>)!</p>",
            "Calculate the average of three test scores (<code>92</code>, <code>85</code>, and <code>98</code>) using <code>3.0</code> so you get the exact decimal average, and display it on the screen!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class MathLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    int coins = 17;
                    int pricePerPotion = 5;
                    int potionsBought = coins / pricePerPotion;
                    int coinsLeftOver = coins % pricePerPotion;

                    Console.WriteLine($"Potions bought: {potionsBought}");
                    Console.WriteLine($"Coins remaining: {coinsLeftOver}");

                    var display = new Label
                    {
                        Position = new Vector2(30, 200),
                        FontSize = 18,
                        Color = "#dcdcaa",
                        Text = $"Bought {potionsBought} potions, {coinsLeftOver} coins left!"
                    };
                    AddChild(display);
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 3: Conditionals (if, else if, else)",
            "if / else, ==, !=, <, >, && (AND), || (OR)",
            "<p>Programs make decisions using <code>if</code>, <code>else if</code>, and <code>else</code> statements.</p>" +
            "<ul>" +
            "<li><code>==</code> checks if two values are equal</li>" +
            "<li><code>&amp;&amp;</code> means <b>AND</b> (both must be true)</li>" +
            "<li><code>||</code> means <b>OR</b> (at least one must be true)</li>" +
            "</ul>",
            "Change <code>temperature</code> to <code>95</code> or <code>30</code>, and add an <code>else if (temperature >= 85)</code> branch that turns the box red (<code>\"#f44747\"</code>) and says <code>\"It's hot!\"</code>!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class ConditionalsLesson : Node2D
            {
                public override void _Ready()
                {
                    int temperature = 72;

                    Position = new Vector2(190, 160);
                    Size = new Vector2(120, 120);

                    var status = new Label { Position = new Vector2(110, 330), FontSize = 20 };
                    AddChild(status);

                    if (temperature < 50)
                    {
                        Color = "#569cd6";
                        status.Text = $"{temperature}°F — Bundle up, it's cold!";
                    }
                    else
                    {
                        Color = "#4ec9b0";
                        status.Text = $"{temperature}°F — Nice weather!";
                    }

                    Console.WriteLine(status.Text);
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 4: Loops (for, while, foreach)",
            "for loops, while loops, grid math",
            "<p>Instead of copy-pasting code 10 times, C# uses <b>loops</b> to repeat actions automatically:</p>" +
            "<p><code>for (int i = 0; i &lt; 8; i++)</code> starts a counter <code>i</code> at 0 and repeats while <code>i &lt; 8</code>, adding 1 each time (<code>i++</code>).</p>" +
            "<p>Watch how a single <code>for</code> loop builds an entire row of rainbow blocks on the canvas!</p>",
            "Change the loop to create <b>10 blocks</b> instead of 7, and use a nested <code>for</code> loop (a loop inside a loop) to create a 2D grid of blocks!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class LoopsLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;
                    string[] colors = { "#f44747", "#ce9178", "#dcdcaa", "#4ec9b0", "#569cd6", "#c586c0", "#4fc1ff" };

                    for (int i = 0; i < 7; i++)
                    {
                        Console.WriteLine($"Creating block #{i}");

                        var block = new Node2D
                        {
                            Position = new Vector2(45 + i * 60, 210),
                            Size = new Vector2(48, 48),
                            Color = colors[i % colors.Length]
                        };
                        AddChild(block);
                    }
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 5: Lists & Collections (List<T>)",
            "List<string>, .Add(), .Count, foreach",
            "<p>A <code>List&lt;T&gt;</code> is a flexible container that can grow and shrink while your program runs. You can add items with <code>.Add(...)</code> and loop over every item with <code>foreach</code>.</p>",
            "Add two more items to <code>inventory</code> (like <code>\"Grappling Hook\"</code> and <code>\"Golden Key\"</code>) using <code>inventory.Add(...)</code> and watch the on-screen list update!",
            """
            using System;
            using System.Collections.Generic;
            using NodeCraftCSharp.Engine;

            public class ListsLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    List<string> inventory = new List<string>();
                    inventory.Add("Wooden Sword");
                    inventory.Add("Health Potion");
                    inventory.Add("Magic Shield");

                    Console.WriteLine($"Total items in backpack: {inventory.Count}");

                    int row = 0;
                    foreach (string item in inventory)
                    {
                        AddChild(new Label
                        {
                            Position = new Vector2(60, 120 + row * 40),
                            FontSize = 20,
                            Color = "#ce9178",
                            Text = $"{row + 1}. {item}"
                        });
                        row++;
                    }
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 6: Writing Reusable Methods",
            "Custom methods, parameters, return values",
            "<p><b>Methods</b> let you package reusable code under a clear name. A method can take <b>parameters</b> (inputs) and return a result (or <code>void</code> if it doesn't return anything).</p>" +
            "<p>Here, <code>SpawnBox(float x, float y, string color)</code> lets us place squares anywhere with one clean line of code!</p>",
            "Write a new method called <code>CalculateBonus(int points, int multiplier)</code> that returns <code>points * multiplier</code>, and print the result to the Console!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class MethodsLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    SpawnBox(80, 120, "#569cd6");
                    SpawnBox(220, 220, "#c586c0");
                    SpawnBox(360, 120, "#4ec9b0");
                    SpawnBox(220, 340, "#dcdcaa");
                }

                private void SpawnBox(float x, float y, string hexColor)
                {
                    AddChild(new Node2D
                    {
                        Position = new Vector2(x, y),
                        Size = new Vector2(60, 60),
                        Color = hexColor
                    });
                }
            }
            """
        ),
        new(
            "csharp",
            "C# 7: Classes, Objects & Inheritance",
            "class, properties, inheritance (: Node2D)",
            "<p>C# is an <b>Object-Oriented</b> language. A <code>class</code> is a blueprint, and each copy you create with <code>new</code> is an <b>object</b>.</p>" +
            "<p>Using <b>inheritance</b> (<code>public class PatrolGuard : Node2D</code>), your custom class automatically inherits <code>Position</code>, <code>Size</code>, and <code>Color</code> from <code>Node2D</code> while adding its own <code>Speed</code> behavior!</p>",
            "Spawn a 3rd <code>PatrolGuard</code> object at <code>y = 360</code> with <code>Speed = 320f</code> and color <code>\"#c586c0\"</code>!",
            """
            using NodeCraftCSharp.Engine;

            public class PatrolGuard : Node2D
            {
                public float Speed = 150f;

                public override void _Process(double delta)
                {
                    Position += new Vector2(Speed * (float)delta, 0);
                    if (Position.X <= 20 || Position.X >= 440)
                        Speed = -Speed;
                }
            }

            public class ClassesLesson : Node2D
            {
                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    AddChild(new PatrolGuard { Position = new Vector2(40, 120), Color = "#f44747", Speed = 140f });
                    AddChild(new PatrolGuard { Position = new Vector2(300, 240), Color = "#4ec9b0", Speed = -220f });
                }
            }
            """
        ),

        // =========================================================================
        // PART 2: 2D GAME ENGINE, SPRITES & AUDIO COURSE (LESSONS 8 - 15)
        // =========================================================================
        new(
            "engine",
            "Game 1: Nodes, Scene Tree & _Ready()",
            "Node, Node2D, Label, Vector2, AddChild()",
            "<p>Now let's put your C# skills into the <b>2D Game Engine</b>! Every object in a scene is a <b>Node</b> arranged in a parent-child <b>Scene Tree</b>.</p>" +
            "<p><code>(0, 0)</code> is the top-left corner of the 500×500 viewport, and <code>(250, 250)</code> is the dead center.</p>",
            "Add a second wall obstacle at <code>new Vector2(320, 150)</code> with color <code>\"#c586c0\"</code> and attach it with <code>AddChild()</code>!",
            """
            using NodeCraftCSharp.Engine;

            public class SceneTreeGame : Node2D
            {
                private Node2D _hero = new();
                private Node2D _wall1 = new();
                private Label _title = new();

                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    _title.Position = new Vector2(20, 32);
                    _title.Text = "Scene Tree Ready!";
                    AddChild(_title);

                    _hero.Position = new Vector2(80, 220);
                    _hero.Color = "#569cd6";
                    AddChild(_hero);

                    _wall1.Position = new Vector2(210, 170);
                    _wall1.Size = new Vector2(30, 150);
                    _wall1.Color = "#ce9178";
                    AddChild(_wall1);
                }
            }
            """
        ),
        new(
            "engine",
            "Game 2: The Game Loop (_Process) & Bouncing Walls",
            "_Process(double delta), Velocity, Wall Bounce",
            "<p>The engine calls <code>_Process(double delta)</code> ~60 times per second. Multiplying velocity by <code>(float)delta</code> guarantees smooth, consistent motion on every computer.</p>",
            "Add an <code>if</code> check for <code>Position.Y</code> so the square also bounces off the top (<code>0</code>) and bottom (<code>500 - Size.Y</code>) edges!",
            """
            using NodeCraftCSharp.Engine;

            public class BouncingLogoGame : Node2D
            {
                public Vector2 Velocity = new Vector2(210f, 165f);

                public override void _Ready()
                {
                    Position = new Vector2(120, 140);
                    Size = new Vector2(48, 48);
                    Color = "#4fc1ff";
                }

                public override void _Process(double delta)
                {
                    Position += Velocity * (float)delta;

                    if (Position.X <= 0 || Position.X >= 500 - Size.X)
                        Velocity = new Vector2(-Velocity.X, Velocity.Y);

                    // Add top and bottom bounce below!
                }
            }
            """
        ),
        new(
            "engine",
            "Game 3: Keyboard Input & Normalized Vectors",
            "Input.IsActionPressed(), Normalized(), Math.Clamp()",
            "<p><code>Input.IsActionPressed(\"ui_right\")</code> works with both <b>Arrow Keys</b> and <b>WASD</b>. Calling <code>dir.Normalized()</code> prevents diagonal movement from being faster than horizontal or vertical movement.</p>",
            "Add <code>ui_up</code> and <code>ui_down</code> for full 4-way movement, plus a Spacebar (<code>ui_accept</code>) sprint boost at <code>420f</code> speed!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class PlayerInputGame : Node2D
            {
                public float Speed = 220f;

                public override void _Ready()
                {
                    Position = new Vector2(230, 230);
                    Color = "#4ec9b0";
                }

                public override void _Process(double delta)
                {
                    Vector2 dir = Vector2.Zero;
                    if (Input.IsActionPressed("ui_right")) dir.X += 1;
                    if (Input.IsActionPressed("ui_left"))  dir.X -= 1;
                    // Add ui_up and ui_down below!

                    float activeSpeed = Input.IsActionPressed("ui_accept") ? 420f : Speed;
                    Position += dir.Normalized() * activeSpeed * (float)delta;

                    Position = new Vector2(
                        Math.Clamp(Position.X, 0, 500 - Size.X),
                        Math.Clamp(Position.Y, 0, 500 - Size.Y)
                    );
                }
            }
            """
        ),
        new(
            "engine",
            "Game 4: Pixel Sprites & Sound Effects (Asset Library)",
            "Sprite2D, Audio.Play(), Asset Library",
            "<p>Upgrade your colored squares to real pixel-art graphics using <code>Sprite2D</code> and add retro sound effects with <code>Audio.Play(\"coin\")</code>!</p>" +
            "<p>Click <b>🎨 Asset Library</b> in the top toolbar anytime to preview all built-in sprites and sound effects and insert them directly into your code.</p>",
            "Open the <b>🎨 Asset Library</b> at the top, insert a <code>\"tree\"</code> or <code>\"gem\"</code> sprite into the scene, and play <code>Audio.Play(\"powerup\")</code> when collected!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class SpriteAdventureGame : Node2D
            {
                private Sprite2D _knight = new() { Texture = "knight", Size = new Vector2(48, 48) };
                private Sprite2D _coin = new() { Texture = "coin", Size = new Vector2(32, 32) };
                private Sprite2D _slime = new() { Texture = "slime", Size = new Vector2(44, 44) };
                private Label _hud = new() { Position = new Vector2(16, 30), Text = "Coins: 0" };
                private Random _rng = new();
                private int _coins = 0;

                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    _knight.Position = new Vector2(225, 225);
                    AddChild(_knight);

                    _coin.Position = new Vector2(120, 120);
                    AddChild(_coin);

                    _slime.Position = new Vector2(360, 340);
                    AddChild(_slime);

                    AddChild(_hud);
                }

                public override void _Process(double delta)
                {
                    Vector2 dir = Vector2.Zero;
                    if (Input.IsActionPressed("ui_right")) dir.X += 1;
                    if (Input.IsActionPressed("ui_left"))  dir.X -= 1;
                    if (Input.IsActionPressed("ui_down"))  dir.Y += 1;
                    if (Input.IsActionPressed("ui_up"))    dir.Y -= 1;

                    _knight.Position += dir.Normalized() * 240f * (float)delta;

                    if (_knight.IsCollidingWith(_coin))
                    {
                        _coins++;
                        _hud.Text = $"Coins: {_coins}";
                        Audio.Play("coin");
                        _coin.Position = new Vector2(_rng.Next(30, 440), _rng.Next(50, 440));
                    }
                }
            }
            """
        ),
        new(
            "engine",
            "Game 5: Collision Detection — Coin Collector",
            "IsCollidingWith(), Random, Score HUD",
            "<p><code>IsCollidingWith(otherNode)</code> detects when two <code>Node2D</code> rectangles overlap. Click inside the game viewport and use Arrow Keys or WASD to collect the gold coins!</p>",
            "Increase <code>_speed</code> by <code>20f</code> every time you grab a coin and call <code>Audio.Play(\"coin\")</code>!",
            """
            using System;
            using NodeCraftCSharp.Engine;

            public class CoinCollectorGame : Node2D
            {
                private Node2D _player = new();
                private Node2D _coin = new();
                private Label _scoreLabel = new();
                private Random _rng = new();
                private int _score = 0;
                private float _speed = 230f;

                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    _player.Position = new Vector2(230, 230);
                    _player.Color = "#569cd6";
                    AddChild(_player);

                    _coin.Size = new Vector2(24, 24);
                    _coin.Color = "#dcdcaa";
                    MoveCoin();
                    AddChild(_coin);

                    _scoreLabel.Position = new Vector2(16, 30);
                    _scoreLabel.Text = "Score: 0";
                    AddChild(_scoreLabel);
                }

                public override void _Process(double delta)
                {
                    Vector2 dir = Vector2.Zero;
                    if (Input.IsActionPressed("ui_right")) dir.X += 1;
                    if (Input.IsActionPressed("ui_left"))  dir.X -= 1;
                    if (Input.IsActionPressed("ui_down"))  dir.Y += 1;
                    if (Input.IsActionPressed("ui_up"))    dir.Y -= 1;

                    _player.Position += dir.Normalized() * _speed * (float)delta;

                    if (_player.IsCollidingWith(_coin))
                    {
                        _score++;
                        _scoreLabel.Text = $"Score: {_score}";
                        Console.WriteLine($"Collected coin! Score = {_score}");
                        MoveCoin();
                    }
                }

                private void MoveCoin()
                {
                    _coin.Position = new Vector2(_rng.Next(20, 450), _rng.Next(50, 450));
                }
            }
            """
        ),
        new(
            "engine",
            "Game 6: Spawning & Destroying Nodes — Space Blaster",
            "QueueFree(), Sprite2D, Audio.Play(), Cooldowns",
            "<p>Games spawn bullets or enemies with <code>AddChild()</code> and safely remove them when off-screen using <code>QueueFree()</code>.</p>",
            "Add a moving enemy target at the top of the screen and destroy it with <code>Audio.Play(\"explosion\")</code> when any <code>Laser</code> collides with it!",
            """
            using System.Linq;
            using NodeCraftCSharp.Engine;

            public class Laser : Sprite2D
            {
                public override void _Ready()
                {
                    Texture = "laser";
                    Size = new Vector2(12, 26);
                }

                public override void _Process(double delta)
                {
                    Position += new Vector2(0, -460f * (float)delta);
                    if (Position.Y < -30)
                        QueueFree();
                }
            }

            public class SpaceBlasterGame : Node2D
            {
                private Sprite2D _ship = new() { Texture = "spaceship", Size = new Vector2(48, 48) };
                private Label _hud = new();
                private double _cooldown = 0;

                public override void _Ready()
                {
                    Size = Vector2.Zero;
                    _ship.Position = new Vector2(226, 425);
                    AddChild(_ship);

                    _hud.Position = new Vector2(14, 28);
                    _hud.FontSize = 14;
                    AddChild(_hud);
                }

                public override void _Process(double delta)
                {
                    if (Input.IsActionPressed("ui_left"))  _ship.Position += new Vector2(-300f * (float)delta, 0);
                    if (Input.IsActionPressed("ui_right")) _ship.Position += new Vector2(300f * (float)delta, 0);

                    _cooldown -= delta;
                    if (Input.IsActionPressed("ui_accept") && _cooldown <= 0)
                    {
                        AddChild(new Laser { Position = new Vector2(_ship.Position.X + 18, _ship.Position.Y - 22) });
                        Audio.Play("laser");
                        _cooldown = 0.22;
                    }

                    int active = Children.OfType<Laser>().Count();
                    _hud.Text = $"A/D or Arrows: Move | SPACE: Fire | Active Lasers: {active}";
                }
            }
            """
        ),
        new(
            "engine",
            "Game 7: Gravity, Jumping & Platformer Physics",
            "Gravity Acceleration, Vertical Velocity, Ground Check",
            "<p>Platformers apply downward gravity to <code>_velocityY</code> every frame, and launch upward with a negative velocity when Spacebar is pressed on the ground.</p>",
            "Add a floating green platform at <code>new Vector2(260, 310)</code> (Size <code>140 x 18</code>) so the player can jump onto it!",
            """
            using NodeCraftCSharp.Engine;

            public class PlatformerGame : Node2D
            {
                private Sprite2D _player = new() { Texture = "knight", Size = new Vector2(44, 48) };
                private Node2D _floor = new();
                private float _velocityY = 0f;
                private float _gravity = 980f;
                private float _jumpForce = -470f;
                private bool _onGround = false;

                public override void _Ready()
                {
                    Size = Vector2.Zero;

                    _floor.Position = new Vector2(0, 440);
                    _floor.Size = new Vector2(500, 60);
                    _floor.Color = "#4ec9b0";
                    AddChild(_floor);

                    _player.Position = new Vector2(80, 300);
                    AddChild(_player);
                }

                public override void _Process(double delta)
                {
                    float dt = (float)delta;

                    if (Input.IsActionPressed("ui_left"))  _player.Position += new Vector2(-240f * dt, 0);
                    if (Input.IsActionPressed("ui_right")) _player.Position += new Vector2(240f * dt, 0);

                    if (Input.IsActionPressed("ui_accept") && _onGround)
                    {
                        _velocityY = _jumpForce;
                        _onGround = false;
                        Audio.Play("jump");
                    }

                    _velocityY += _gravity * dt;
                    _player.Position += new Vector2(0, _velocityY * dt);

                    if (_player.Position.Y + _player.Size.Y >= _floor.Position.Y)
                    {
                        _player.Position = new Vector2(_player.Position.X, _floor.Position.Y - _player.Size.Y);
                        _velocityY = 0;
                        _onGround = true;
                    }
                }
            }
            """
        ),
        new(
            "engine",
            "Game 8: Advanced C# — LINQ, JSON & External Libraries",
            "System.Linq, System.Text.Json, External .dlls",
            "<p>Hold <b>Spacebar</b> in the game viewport to spray bouncing particles while <b>LINQ</b> and <code>JsonSerializer</code> analyze the live scene in real time!</p>",
            "Use LINQ (<code>.Count(p => p.Color == \"#f44747\")</code>) to count red particles and display that count in the HUD!",
            """
            using System;
            using System.Linq;
            using System.Text.Json;
            using NodeCraftCSharp.Engine;

            public class Particle : Node2D
            {
                public Vector2 Velocity;
                public double LifeLeft = 3.5;

                public override void _Process(double delta)
                {
                    LifeLeft -= delta;
                    if (LifeLeft <= 0)
                    {
                        QueueFree();
                        return;
                    }

                    Velocity += new Vector2(0, 320f * (float)delta);
                    Position += Velocity * (float)delta;

                    if (Position.Y > 485)
                    {
                        Position = new Vector2(Position.X, 485);
                        Velocity = new Vector2(Velocity.X, -Velocity.Y * 0.75f);
                    }
                }
            }

            public class FullCSharpDemo : Node2D
            {
                private Label _hud = new();
                private Random _rng = new();
                private string[] _palette = { "#f44747", "#569cd6", "#4ec9b0", "#c586c0", "#dcdcaa" };

                public override void _Ready()
                {
                    Size = Vector2.Zero;
                    _hud.Position = new Vector2(12, 26);
                    _hud.FontSize = 13;
                    AddChild(_hud);
                    SpawnBurst(14);
                }

                public override void _Process(double delta)
                {
                    if (Input.IsActionPressed("ui_accept"))
                        SpawnBurst(3);

                    var particles = Children.OfType<Particle>().ToList();
                    string jsonStats = JsonSerializer.Serialize(new
                    {
                        Active = particles.Count,
                        AvgY = particles.Any() ? (int)particles.Average(p => p.Position.Y) : 0
                    });

                    _hud.Text = $"Hold SPACE to spray! | JSON: {jsonStats}";
                }

                private void SpawnBurst(int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        AddChild(new Particle
                        {
                            Position = new Vector2(245, 220),
                            Size = new Vector2(10, 10),
                            Color = _palette[_rng.Next(_palette.Length)],
                            Velocity = new Vector2(_rng.Next(-220, 221), _rng.Next(-340, -120))
                        });
                    }
                }
            }
            """
        )
    };

    protected override void OnInitialized()
    {
        _code = _lessons[0].StarterCode;

        Console.SetOut(new ConsoleRedirector(line =>
        {
            if (_consoleLogs.Count < 200)
            {
                _consoleLogs.Add(line);
                _ = InvokeAsync(StateHasChanged);
            }
        }));

        _tree.OnRuntimeError = msg =>
        {
            _errors = new List<string> { msg };
            _ = InvokeAsync(StateHasChanged);
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("gameEngineBridge.initEditor", "codeEditorTextarea", "codeHighlightContent", "lineNumbersGutter", _dotNetRef);
            await JS.InvokeVoidAsync("gameEngineBridge.initCanvas", "gameCanvas", _dotNetRef);

            try
            {
                await _compiler.InitializeAsync(Http);
                _isReady = true;
                await RunCodeAsync();
            }
            catch (Exception ex)
            {
                _errors = new List<string> { $"Compiler startup failed: {ex.Message}" };
            }

            StateHasChanged();

            _loopCts = new CancellationTokenSource();
            _ = RunGameLoopAsync(_loopCts.Token);
        }
        else
        {
            if (_needsCanvasReinit)
            {
                _needsCanvasReinit = false;
                await JS.InvokeVoidAsync("gameEngineBridge.initCanvas", "gameCanvas", _dotNetRef);
            }

            if (_needsHighlightSync)
            {
                _needsHighlightSync = false;
                await JS.InvokeVoidAsync("gameEngineBridge.syncEditorHighlight");
            }

            if (_needsSpritePreviewRender)
            {
                _needsSpritePreviewRender = false;
                await RenderSpritePreviewsAsync();
            }
        }
    }

    private async Task SwitchToGameLessonsMode()
    {
        if (!_isScratchpadMode) return;
        _isScratchpadMode = false;
        _code = _lessons[_selectedLesson].StarterCode;
        _needsCanvasReinit = true;
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private async Task SwitchToScratchpadMode()
    {
        if (_isScratchpadMode) return;
        _isScratchpadMode = true;
        _code = _scratchpadTemplates[_selectedScratchpad].StarterCode;
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private async Task SelectScratchpadTemplate(ChangeEventArgs e)
    {
        _selectedScratchpad = int.Parse(e.Value?.ToString() ?? "0");
        _code = _scratchpadTemplates[_selectedScratchpad].StarterCode;
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private void ToggleAssetLibrary()
    {
        _showAssetLibrary = !_showAssetLibrary;
        if (_showAssetLibrary && _assetTab == "sprites")
            _needsSpritePreviewRender = true;
    }

    private void SetAssetTab(string tab)
    {
        _assetTab = tab;
        if (tab == "sprites")
            _needsSpritePreviewRender = true;
    }

    private async Task RenderSpritePreviewsAsync()
    {
        foreach (var sprite in _builtInSprites)
        {
            await JS.InvokeVoidAsync("gameEngineBridge.renderSpritePreview", $"preview_{sprite.Id}", sprite.Id);
        }
    }

    private async Task PreviewSound(string soundId)
    {
        await JS.InvokeVoidAsync("gameEngineBridge.playSound", soundId);
    }

    private async Task InsertSnippetAtCursor(string snippet)
    {
        await JS.InvokeVoidAsync("gameEngineBridge.insertSnippet", snippet);
    }

    private async Task OnCodeInput(ChangeEventArgs e)
    {
        _code = e.Value?.ToString() ?? "";
        await JS.InvokeVoidAsync("gameEngineBridge.syncEditorHighlight");
    }

    private void SetTrackFilter(string track)
    {
        _trackFilter = track;
        for (int i = 0; i < _lessons.Count; i++)
        {
            if (_trackFilter == "all" || _lessons[i].Track == _trackFilter)
            {
                _selectedLesson = i;
                _code = _lessons[i].StarterCode;
                _needsHighlightSync = true;
                _ = RunCodeAsync();
                break;
            }
        }
    }

    private async Task SelectLesson(ChangeEventArgs e)
    {
        _selectedLesson = int.Parse(e.Value?.ToString() ?? "0");
        _code = _lessons[_selectedLesson].StarterCode;
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private async Task PrevLesson()
    {
        if (_selectedLesson > 0)
        {
            _selectedLesson--;
            _code = _lessons[_selectedLesson].StarterCode;
            _needsHighlightSync = true;
            await RunCodeAsync();
        }
    }

    private async Task NextLesson()
    {
        if (_selectedLesson < _lessons.Count - 1)
        {
            _selectedLesson++;
            _code = _lessons[_selectedLesson].StarterCode;
            _needsHighlightSync = true;
            await RunCodeAsync();
        }
    }

    private async Task ResetCurrentTemplate()
    {
        _code = _isScratchpadMode
            ? _scratchpadTemplates[_selectedScratchpad].StarterCode
            : _lessons[_selectedLesson].StarterCode;
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private void ClearConsole() => _consoleLogs.Clear();

    private Task RunCode() => RunCodeAsync();

    private async Task RunCodeAsync()
    {
        if (!_isReady) return;

        _consoleLogs.Clear();
        var result = _compiler.CompileAndRun(_code);
        _errors = result.Diagnostics;

        if (result.Success && result.Mode == ExecutionMode.GameScene && result.EntryScene != null)
        {
            _tree.LoadMainScene(result.EntryScene);
            if (!_isScratchpadMode)
            {
                _tree.Step(0.0, _renderContext);
                await JS.InvokeVoidAsync("gameEngineBridge.renderFrame", _renderContext.Commands);
            }
        }
        else if (!_isScratchpadMode)
        {
            _renderContext.Reset();
            _renderContext.Clear("#1e1e1e");
            await JS.InvokeVoidAsync("gameEngineBridge.renderFrame", _renderContext.Commands);
        }

        StateHasChanged();
    }

    private async Task RunGameLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        var lastTime = DateTime.UtcNow;

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!_isReady || _isScratchpadMode || _errors.Count > 0) continue;

                var now = DateTime.UtcNow;
                double delta = (now - lastTime).TotalSeconds;
                lastTime = now;

                _tree.Step(delta, _renderContext);
                await JS.InvokeVoidAsync("gameEngineBridge.renderFrame", ct, _renderContext.Commands);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task DownloadCode()
    {
        string fileName = _isScratchpadMode ? "Program.cs" : $"Lesson{_selectedLesson + 1}_MyGame.cs";
        await JS.InvokeVoidAsync("gameEngineBridge.downloadTextFile", fileName, _code);
    }

    private async Task UploadCode(InputFileChangeEventArgs e)
    {
        using var stream = e.File.OpenReadStream(maxAllowedSize: 512 * 1024);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        _code = await reader.ReadToEndAsync();
        _needsHighlightSync = true;
        await RunCodeAsync();
    }

    private async Task UploadLibrary(InputFileChangeEventArgs e)
    {
        using var stream = e.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        _compiler.AddExternalLibrary(e.File.Name, ms.ToArray());
        await RunCodeAsync();
    }

    [JSInvokable]
    public void OnKeyChanged(string key, bool isDown) => Input.SetKeyState(key, isDown);

    [JSInvokable]
    public void OnEditorTextChanged(string updatedCode) => _code = updatedCode;

    [JSInvokable]
    public async Task OnEditorRunShortcut() => await RunCodeAsync();

    public async ValueTask DisposeAsync()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _dotNetRef?.Dispose();
        await Task.CompletedTask;
    }
}
