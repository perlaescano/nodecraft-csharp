using System;
using System.Collections.Generic;
using System.Linq;

namespace NodeCraftCSharp.Engine;

public struct Vector2
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);
    public static Vector2 Up => new(0, -1);
    public static Vector2 Down => new(0, 1);
    public static Vector2 Left => new(-1, 0);
    public static Vector2 Right => new(1, 0);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, float scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vector2 operator *(float scalar, Vector2 a) => new(a.X * scalar, a.Y * scalar);
    public static Vector2 operator /(Vector2 a, float scalar) => new(a.X / scalar, a.Y / scalar);

    public float Length() => MathF.Sqrt(X * X + Y * Y);

    public Vector2 Normalized()
    {
        float len = Length();
        return len > 0.0001f ? new Vector2(X / len, Y / len) : Zero;
    }

    public float DistanceTo(Vector2 other) => (other - this).Length();

    public override string ToString() => $"({X:F1}, {Y:F1})";
}

public static class Input
{
    private static readonly HashSet<string> _pressedKeys = new(StringComparer.OrdinalIgnoreCase);

    public static void SetKeyState(string key, bool isDown)
    {
        if (isDown) _pressedKeys.Add(key);
        else _pressedKeys.Remove(key);
    }

    public static void ClearAll() => _pressedKeys.Clear();

    public static bool IsKeyPressed(string key) => _pressedKeys.Contains(key);

    /// <summary>
    /// Built-in input actions: "ui_left", "ui_right", "ui_up", "ui_down", "ui_accept"
    /// </summary>
    public static bool IsActionPressed(string action) => action.ToLowerInvariant() switch
    {
        "ui_left" => IsKeyPressed("ArrowLeft") || IsKeyPressed("a"),
        "ui_right" => IsKeyPressed("ArrowRight") || IsKeyPressed("d"),
        "ui_up" => IsKeyPressed("ArrowUp") || IsKeyPressed("w"),
        "ui_down" => IsKeyPressed("ArrowDown") || IsKeyPressed("s"),
        "ui_accept" => IsKeyPressed(" ") || IsKeyPressed("Enter"),
        _ => IsKeyPressed(action)
    };
}

/// <summary>
/// Built-in retro arcade sound synthesizer.
/// Available sounds: "coin", "jump", "laser", "explosion", "powerup", "hit"
/// </summary>
public static class Audio
{
    private static readonly List<string> _pendingSounds = new();

    public static void Play(string soundName)
    {
        if (!string.IsNullOrWhiteSpace(soundName) && _pendingSounds.Count < 8)
            _pendingSounds.Add(soundName.Trim().ToLowerInvariant());
    }

    internal static void Clear() => _pendingSounds.Clear();

    internal static List<string> Flush()
    {
        var copy = new List<string>(_pendingSounds);
        _pendingSounds.Clear();
        return copy;
    }
}

public record DrawCommand(
    string Type,
    float X,
    float Y,
    float W,
    float H,
    string Color,
    string? Text = null,
    int FontSize = 16
);

public class FrameRenderContext
{
    private readonly List<DrawCommand> _commands = new();
    public IReadOnlyList<DrawCommand> Commands => _commands;

    public void Reset() => _commands.Clear();

    public void Clear(string hexColor)
        => _commands.Add(new DrawCommand("clear", 0, 0, 0, 0, hexColor));

    public void DrawRect(float x, float y, float width, float height, string hexColor)
        => _commands.Add(new DrawCommand("rect", x, y, width, height, hexColor));

    public void DrawSprite(string spriteName, float x, float y, float width, float height, string tintColor = "")
        => _commands.Add(new DrawCommand("sprite", x, y, width, height, tintColor, spriteName));

    public void DrawCircle(float x, float y, float radius, string hexColor)
        => _commands.Add(new DrawCommand("circle", x, y, radius, 0, hexColor));

    public void DrawLine(float x1, float y1, float x2, float y2, string hexColor, float lineWidth = 2f)
        => _commands.Add(new DrawCommand("line", x1, y1, x2, y2, hexColor, null, (int)lineWidth));

    public void DrawText(string text, float x, float y, string hexColor, int fontSize = 16)
        => _commands.Add(new DrawCommand("text", x, y, 0, 0, hexColor, text, fontSize));

    public void PlaySound(string soundName)
        => _commands.Add(new DrawCommand("sound", 0, 0, 0, 0, "", soundName));
}

public class Node
{
    public string Name { get; set; } = "Node";
    public Node? Parent { get; private set; }
    public List<Node> Children { get; } = new();
    public bool IsQueuedForDeletion { get; private set; }

    public void Print(object? message) => Console.WriteLine(message);

    public void AddChild(Node child)
    {
        child.Parent = this;
        Children.Add(child);
        child._Ready();
    }

    public void RemoveChild(Node child)
    {
        if (Children.Remove(child))
            child.Parent = null;
    }

    public void QueueFree()
    {
        IsQueuedForDeletion = true;
    }

    public T? GetNode<T>(string name) where T : Node
    {
        return Children.OfType<T>().FirstOrDefault(c => c.Name == name);
    }

    public virtual void _Ready() { }
    public virtual void _Process(double delta) { }

    internal void PropagateProcess(double delta)
    {
        if (IsQueuedForDeletion) return;

        _Process(delta);

        foreach (var child in Children.ToArray())
        {
            if (!child.IsQueuedForDeletion)
                child.PropagateProcess(delta);
        }

        Children.RemoveAll(c => c.IsQueuedForDeletion);
    }
}

public class Node2D : Node
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public Vector2 Size { get; set; } = new(40, 40);
    public string Color { get; set; } = "#478CBF";

    /// <summary>
    /// Optional built-in sprite name from the Asset Library (e.g., "knight", "coin", "slime", "spaceship").
    /// </summary>
    public string? Sprite { get; set; }

    /// <summary>
    /// Axis-Aligned Bounding Box (AABB) collision check between two Node2D objects.
    /// </summary>
    public bool IsCollidingWith(Node2D other)
    {
        if (IsQueuedForDeletion || other.IsQueuedForDeletion) return false;

        return Position.X < other.Position.X + other.Size.X &&
               Position.X + Size.X > other.Position.X &&
               Position.Y < other.Position.Y + other.Size.Y &&
               Position.Y + Size.Y > other.Position.Y;
    }

    public virtual void _Draw(FrameRenderContext ctx)
    {
        if (Size.X <= 0 || Size.Y <= 0) return;

        if (!string.IsNullOrWhiteSpace(Sprite))
            ctx.DrawSprite(Sprite.Trim().ToLowerInvariant(), Position.X, Position.Y, Size.X, Size.Y, Color);
        else
            ctx.DrawRect(Position.X, Position.Y, Size.X, Size.Y, Color);
    }

    internal void PropagateDraw(FrameRenderContext ctx)
    {
        if (IsQueuedForDeletion) return;

        _Draw(ctx);
        foreach (var child in Children.ToArray())
        {
            if (child is Node2D n2d && !n2d.IsQueuedForDeletion)
                n2d.PropagateDraw(ctx);
        }
    }
}

/// <summary>
/// Renders a built-in pixel-art sprite from the Asset Library.
/// Available textures: "knight", "slime", "coin", "gem", "spaceship", "laser", "star", "heart", "tree", "crate"
/// </summary>
public class Sprite2D : Node2D
{
    public string Texture
    {
        get => Sprite ?? "knight";
        set => Sprite = value;
    }

    public Sprite2D()
    {
        Sprite = "knight";
        Size = new Vector2(48, 48);
    }
}

public class Label : Node2D
{
    public string Text { get; set; } = "Label";
    public int FontSize { get; set; } = 18;

    public Label()
    {
        Color = "#ffffff";
        Size = Vector2.Zero;
    }

    public override void _Draw(FrameRenderContext ctx)
    {
        ctx.DrawText(Text, Position.X, Position.Y, Color, FontSize);
    }
}

public class SceneTree
{
    public Node Root { get; } = new() { Name = "Root" };
    public Action<string>? OnRuntimeError { get; set; }
    private bool _halted;

    public void LoadMainScene(Node mainScene)
    {
        _halted = false;
        Input.ClearAll();
        Audio.Clear();
        Root.Children.Clear();
        try
        {
            Root.AddChild(mainScene);
        }
        catch (Exception ex)
        {
            _halted = true;
            OnRuntimeError?.Invoke($"Runtime Error in _Ready(): {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public void Step(double delta, FrameRenderContext ctx)
    {
        ctx.Reset();
        ctx.Clear("#1e1e1e");

        if (_halted) return;

        // Clamp delta so switching browser tabs doesn't cause huge physics jumps
        double clampedDelta = Math.Min(delta, 0.05);

        try
        {
            Root.PropagateProcess(clampedDelta);
            foreach (var child in Root.Children.ToArray())
            {
                if (child is Node2D n2d)
                    n2d.PropagateDraw(ctx);
            }

            foreach (var sound in Audio.Flush())
            {
                ctx.PlaySound(sound);
            }
        }
        catch (Exception ex)
        {
            _halted = true;
            OnRuntimeError?.Invoke($"Runtime Error in _Process(): {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
