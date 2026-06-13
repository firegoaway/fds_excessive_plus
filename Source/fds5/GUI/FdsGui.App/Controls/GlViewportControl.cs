using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using FdsGui.Core.Models;
using FdsGui.Viewport;
using FdsGui.Viewport.Picking;
using FdsGui.Viewport.Renderers;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace FdsGui.App.Controls;

/// <summary>
/// OpenGL viewport control with Blender-style camera, wall rendering, tools, and picking.
/// </summary>
public class GlViewportControl : OpenGlControlBase
{
    public Camera Camera { get; } = new();
    public EditorState? Editor { get; set; }

    private readonly GridRenderer _grid = new();

    // OpenGL resources
    private int _gridVao, _gridVbo, _gridVertexCount;
    private int _axisVao, _axisVbo;
    private int _shaderProgram, _uMvp;
    private bool _initialized;

    // Wall GL resources
    private int _wallVao, _wallVbo, _wallEbo, _wallIndexCount;

    // Ghost GL resources
    private int _ghostVao, _ghostVbo, _ghostEbo;

    // Selection outline GL resources
    private int _outlineVao, _outlineVbo;

    // Handle GL resources
    private int _handleVao, _handleVbo, _handleEbo;

    // Mouse state
    private Point _lastMouse;
    private Point _mouseDownPos;
    private bool _isMiddleDown;
    private bool _isLeftDown;
    private bool _isDragging;
    private const double DragThreshold = 5.0;

    // Shader sources
    private const string VertexShaderSource = @"
#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec4 aColor;
out vec4 vColor;
uniform mat4 uMVP;
void main()
{
    gl_Position = uMVP * vec4(aPos, 1.0);
    vColor = aColor;
}
";

    private const string FragmentShaderSource = @"
#version 330 core
in vec4 vColor;
out vec4 FragColor;
void main()
{
    FragColor = vColor;
}
";

    public GlViewportControl()
    {
        Focusable = true;
        DispatcherTimer.Run(() =>
        {
            RequestNextFrameRendering();
            return true;
        }, TimeSpan.FromMilliseconds(16));
    }

    // ===================== GL Lifecycle =====================

    protected override void OnOpenGlInit(GlInterface gl)
    {
        var bindings = new AvaloniaGlBindings(gl);
        GL.LoadBindings(bindings);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1.0f);

        // Compile shader
        int vs = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        int fs = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vs);
        GL.AttachShader(_shaderProgram, fs);
        GL.LinkProgram(_shaderProgram);
        GL.DetachShader(_shaderProgram, vs);
        GL.DetachShader(_shaderProgram, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
        _uMvp = GL.GetUniformLocation(_shaderProgram, "uMVP");

        // Grid
        float[] gridVerts = _grid.GenerateGridVertices();
        _gridVertexCount = gridVerts.Length / 7;
        _gridVao = GL.GenVertexArray();
        GL.BindVertexArray(_gridVao);
        _gridVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _gridVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, gridVerts.Length * sizeof(float), gridVerts, BufferUsageHint.StaticDraw);
        SetupVertexAttribs();
        GL.BindVertexArray(0);

        // Axes
        float[] axisVerts = _grid.GenerateAxisVertices();
        _axisVao = GL.GenVertexArray();
        GL.BindVertexArray(_axisVao);
        _axisVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _axisVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, axisVerts.Length * sizeof(float), axisVerts, BufferUsageHint.StaticDraw);
        SetupVertexAttribs();
        GL.BindVertexArray(0);

        // Wall VAO/VBO/EBO
        _wallVao = GL.GenVertexArray();
        _wallVbo = GL.GenBuffer();
        _wallEbo = GL.GenBuffer();

        // Ghost VAO/VBO/EBO
        _ghostVao = GL.GenVertexArray();
        _ghostVbo = GL.GenBuffer();
        _ghostEbo = GL.GenBuffer();

        // Selection outline VAO/VBO
        _outlineVao = GL.GenVertexArray();
        _outlineVbo = GL.GenBuffer();

        // Handle VAO/VBO/EBO
        _handleVao = GL.GenVertexArray();
        _handleVbo = GL.GenBuffer();
        _handleEbo = GL.GenBuffer();

        _initialized = true;
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (!_initialized) return;
        GL.DeleteBuffer(_gridVbo); GL.DeleteVertexArray(_gridVao);
        GL.DeleteBuffer(_axisVbo); GL.DeleteVertexArray(_axisVao);
        GL.DeleteBuffer(_wallVbo); GL.DeleteBuffer(_wallEbo); GL.DeleteVertexArray(_wallVao);
        GL.DeleteBuffer(_ghostVbo); GL.DeleteBuffer(_ghostEbo); GL.DeleteVertexArray(_ghostVao);
        GL.DeleteBuffer(_outlineVbo); GL.DeleteVertexArray(_outlineVao);
        GL.DeleteBuffer(_handleVbo); GL.DeleteBuffer(_handleEbo); GL.DeleteVertexArray(_handleVao);
        GL.DeleteProgram(_shaderProgram);
        _initialized = false;
    }

    // ===================== Rendering =====================

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_initialized) return;

        var size = Bounds.Size;
        int w = (int)size.Width;
        int h = (int)size.Height;
        if (w <= 0 || h <= 0) return;

        Camera.AspectRatio = (float)(w / (double)h);

        GL.Viewport(0, 0, w, h);
        GL.ClearColor(0.176f, 0.176f, 0.188f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.UseProgram(_shaderProgram);

        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();
        Matrix4 mvp = view * proj;
        GL.UniformMatrix4(_uMvp, false, ref mvp);

        // 1. Grid
        GL.BindVertexArray(_gridVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _gridVertexCount);

        // 2. Axes
        GL.LineWidth(2.5f);
        GL.BindVertexArray(_axisVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, GridRenderer.AxisVertexCount);
        GL.LineWidth(1.0f);

        // 3. Walls
        if (Editor != null)
        {
            RenderWalls();
            RenderGhost();
            RenderSelection();
        }

        GL.BindVertexArray(0);
    }

    private void RenderWalls()
    {
        if (Editor == null) return;
        var walls = Editor.ActiveFloor.Walls;
        if (walls.Count == 0) return;

        if (Editor.WallsDirty || _wallIndexCount == 0)
        {
            RebuildWallBuffer(walls);
            Editor.WallsDirty = false;
        }

        if (_wallIndexCount > 0)
        {
            GL.BindVertexArray(_wallVao);
            GL.DrawElements(PrimitiveType.Triangles, _wallIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RebuildWallBuffer(List<Wall> walls)
    {
        if (walls.Count == 0) { _wallIndexCount = 0; return; }

        var (vertices, indices) = WallRenderer.GenerateBatchedGeometry(walls);
        _wallIndexCount = indices.Length;

        GL.BindVertexArray(_wallVao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _wallVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        SetupVertexAttribs();

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _wallEbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(ushort), indices, BufferUsageHint.DynamicDraw);

        GL.BindVertexArray(0);
    }

    private void RenderGhost()
    {
        if (Editor == null || !Editor.GhostRenderer.HasGhost) return;

        var verts = Editor.GhostRenderer.GenerateGhostVertices();
        var indices = Editor.GhostRenderer.GetGhostIndices();
        if (verts == null) return;

        GL.BindVertexArray(_ghostVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _ghostVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.DynamicDraw);
        SetupVertexAttribs();

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ghostEbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(ushort), indices, BufferUsageHint.DynamicDraw);

        GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedShort, 0);
        GL.BindVertexArray(0);
    }

    private void RenderSelection()
    {
        if (Editor == null || Editor.SelectedWalls.Count == 0) return;

        foreach (var wall in Editor.SelectedWalls)
        {
            // Outline (wireframe edges)
            var outlineVerts = SelectionRenderer.GenerateOutlineVertices(wall);
            if (outlineVerts != null)
            {
                var edgeIndices = SelectionRenderer.GetOutlineEdgeIndices();

                GL.BindVertexArray(_outlineVao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _outlineVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, outlineVerts.Length * sizeof(float), outlineVerts, BufferUsageHint.DynamicDraw);
                SetupVertexAttribs();

                GL.LineWidth(2.0f);
                GL.DrawElements(PrimitiveType.Lines, edgeIndices.Length, DrawElementsType.UnsignedShort, 0);
                GL.LineWidth(1.0f);
                GL.BindVertexArray(0);
            }

            // Handles
            var handleVerts = SelectionRenderer.GenerateHandleVertices(wall);
            var handleIndices = SelectionRenderer.GetHandleIndices();

            GL.BindVertexArray(_handleVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _handleVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, handleVerts.Length * sizeof(float), handleVerts, BufferUsageHint.DynamicDraw);
            SetupVertexAttribs();

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _handleEbo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, handleIndices.Length * sizeof(ushort), handleIndices, BufferUsageHint.DynamicDraw);

            GL.DrawElements(PrimitiveType.Triangles, handleIndices.Length, DrawElementsType.UnsignedShort, 0);
            GL.BindVertexArray(0);
        }
    }

    private void SetupVertexAttribs()
    {
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
    }

    // ===================== Input: Pointer =====================

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var pos = e.GetPosition(this);
        _lastMouse = pos;
        _mouseDownPos = pos;
        _isDragging = false;

        var props = e.GetCurrentPoint(this).Properties;
        _isMiddleDown = props.IsMiddleButtonPressed;
        _isLeftDown = props.IsLeftButtonPressed;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var delta = pos - _lastMouse;

        // Check drag threshold
        if (!_isDragging && _isLeftDown)
        {
            var totalDelta = pos - _mouseDownPos;
            if (Math.Sqrt(totalDelta.X * totalDelta.X + totalDelta.Y * totalDelta.Y) > DragThreshold)
                _isDragging = true;
        }

        // Camera controls
        if (_isMiddleDown)
        {
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (shift) Camera.Pan((float)delta.X, (float)delta.Y);
            else Camera.Orbit((float)delta.X, (float)delta.Y);
        }
        else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            Camera.Pan((float)delta.X, (float)delta.Y);
        }

        // Tool interactions (left mouse drag)
        if (_isLeftDown && _isDragging && Editor != null)
        {
            var groundPos = GetGroundPosition(pos);

            switch (Editor.CurrentTool)
            {
                case ToolMode.Wall:
                    var snappedPos = SnapPosition(groundPos);
                    Editor.UpdateWallDraw(snappedPos);
                    break;

                case ToolMode.Select when Editor.ActiveTransform == TransformMode.Move:
                    var snappedMove = SnapPosition(groundPos);
                    Editor.UpdateMove(snappedMove);
                    break;

                case ToolMode.Select when Editor.ActiveTransform == TransformMode.Rotate:
                    Editor.UpdateRotate(groundPos);
                    break;
            }
        }

        _lastMouse = pos;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (Editor == null) { _isMiddleDown = false; _isLeftDown = false; return; }

        var pos = e.GetPosition(this);
        var groundPos = GetGroundPosition(pos);

        if (_isLeftDown)
        {
            if (Editor.ActiveTransform != TransformMode.None)
            {
                // Confirm transform on click
                if (Editor.ActiveTransform == TransformMode.Move) Editor.ConfirmMove();
                else if (Editor.ActiveTransform == TransformMode.Rotate) Editor.ConfirmRotate();
            }
            else if (!_isDragging)
            {
                // Click (not drag)
                switch (Editor.CurrentTool)
                {
                    case ToolMode.Wall:
                        var snappedStart = SnapPosition(groundPos);
                        Editor.BeginWallDraw(snappedStart);
                        break;

                    case ToolMode.Select:
                        HandleSelectionClick(pos, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                        break;
                }
            }
            else if (_isDragging && Editor.CurrentTool == ToolMode.Wall)
            {
                // Wall drag release -> create wall
                var snappedEnd = SnapPosition(groundPos);
                Editor.FinishWallDraw(snappedEnd);
            }
        }

        _isMiddleDown = false;
        _isLeftDown = false;
        _isDragging = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera.Zoom((float)e.Delta.Y);
    }

    // ===================== Input: Keyboard =====================

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Editor == null) return;

        // Camera shortcuts
        switch (e.Key)
        {
            case Avalonia.Input.Key.NumPad7: Camera.SetTopView(); return;
            case Avalonia.Input.Key.NumPad1: Camera.SetFrontView(); return;
            case Avalonia.Input.Key.NumPad3: Camera.SetRightView(); return;
            case Avalonia.Input.Key.NumPad5: Camera.ToggleProjection(); return;
            case Avalonia.Input.Key.Home: Camera.FitAll(10.0f); return;
        }

        // Ctrl shortcuts
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.Z:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Editor.Redo();
                    else Editor.Undo();
                    return;
            }
        }

        // Tool shortcuts
        switch (e.Key)
        {
            case Avalonia.Input.Key.Escape:
                if (Editor.ActiveTransform != TransformMode.None)
                {
                    if (Editor.ActiveTransform == TransformMode.Move) Editor.CancelMove();
                    else if (Editor.ActiveTransform == TransformMode.Rotate) Editor.CancelRotate();
                }
                else if (Editor.CurrentTool != ToolMode.Select)
                {
                    Editor.SetTool(ToolMode.Select);
                }
                else
                {
                    Editor.ClearSelection();
                }
                break;

            case Avalonia.Input.Key.W:
                Editor.SetTool(ToolMode.Wall);
                break;

            case Avalonia.Input.Key.G:
                StartMove();
                break;

            case Avalonia.Input.Key.R:
                if (Editor.ActiveTransform == TransformMode.Rotate)
                {
                    Editor.ConfirmRotate();
                }
                else
                {
                    StartRotate();
                }
                break;

            case Avalonia.Input.Key.X:
            case Avalonia.Input.Key.Delete:
                Editor.DeleteSelected();
                break;

            case Avalonia.Input.Key.F4:
                Editor.SnapEngine.IsEnabled = !Editor.SnapEngine.IsEnabled;
                break;

            case Avalonia.Input.Key.OemOpenBrackets: // [
                Editor.SnapEngine.DecreaseGridStep();
                break;

            case Avalonia.Input.Key.OemCloseBrackets: // ]
                Editor.SnapEngine.IncreaseGridStep();
                break;
        }
    }

    // ===================== Public API (called from MainWindow) =====================

    public void StartMove()
    {
        if (Editor?.SingleSelectedWall == null) return;
        // Use wall center as initial cursor position for move
        var wall = Editor.SingleSelectedWall;
        var center = new Point2D(
            (wall.Start.X + wall.End.X) / 2,
            (wall.Start.Y + wall.End.Y) / 2);
        Editor.BeginMove(center);
    }

    public void StartRotate()
    {
        if (Editor?.SingleSelectedWall == null) return;
        var wall = Editor.SingleSelectedWall;
        var center = new Point2D(
            (wall.Start.X + wall.End.X) / 2,
            (wall.Start.Y + wall.End.Y) / 2);
        // Start with a reference angle offset
        var refPoint = new Point2D(center.X + 1.0, center.Y);
        Editor.BeginRotate(refPoint);
    }

    // ===================== Helpers =====================

    private Point2D GetGroundPosition(Point screenPos)
    {
        var size = Bounds.Size;
        int w = (int)size.Width;
        int h = (int)size.Height;
        if (w <= 0 || h <= 0) return new Point2D(0, 0);

        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();
        return ObjectPicker.ScreenToGroundPlane(screenPos.X, screenPos.Y, w, h, view, proj);
    }

    private Point2D SnapPosition(Point2D worldPos)
    {
        if (Editor == null) return worldPos;
        var result = Editor.SnapEngine.Snap(worldPos, Editor.ActiveWalls);
        return result.Position;
    }

    private void HandleSelectionClick(Point screenPos, bool addToSelection)
    {
        if (Editor == null) return;

        var size = Bounds.Size;
        int w = (int)size.Width;
        int h = (int)size.Height;
        if (w <= 0 || h <= 0) return;

        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();

        var wall = ObjectPicker.PickWall(screenPos.X, screenPos.Y, w, h, view, proj, Editor.ActiveWalls);
        Editor.SelectWall(wall, addToSelection);
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
            System.Diagnostics.Debug.WriteLine($"Shader compile error ({type}): {GL.GetShaderInfoLog(shader)}");
        return shader;
    }
}

/// <summary>
/// Bridges Avalonia's GlInterface to OpenTK's IBindingsContext.
/// </summary>
internal class AvaloniaGlBindings : OpenTK.IBindingsContext
{
    private readonly GlInterface _gl;
    public AvaloniaGlBindings(GlInterface gl) => _gl = gl;
    public IntPtr GetProcAddress(string procName) => _gl.GetProcAddress(procName);
}
