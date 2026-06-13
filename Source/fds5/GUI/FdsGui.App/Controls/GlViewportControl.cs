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
    // Door GL resources
    private int _doorVao, _doorVbo, _doorEbo, _doorIndexCount;
    // Slab GL resources
    private int _slabVao, _slabVbo, _slabEbo, _slabIndexCount;
    // Stair GL resources
    private int _stairVao, _stairVbo, _stairEbo, _stairIndexCount;
    // Room GL resources
    private int _roomVao, _roomVbo, _roomEbo, _roomIndexCount;
    // Room outline GL resources
    private int _roomOutlineVao, _roomOutlineVbo;
    // Background GL resources
    private int _bgVao, _bgVbo, _bgEbo, _bgIndexCount;
    // Background outline GL resources
    private int _bgOutlineVao, _bgOutlineVbo;

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

    private const string VertexShaderSource = @"#version 300 es
precision mediump float;
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec4 aColor;
out vec4 vColor;
uniform mat4 uMVP;
void main() { gl_Position = uMVP * vec4(aPos, 1.0); vColor = aColor; }
";

    private const string FragmentShaderSource = @"#version 300 es
precision mediump float;
in vec4 vColor;
out vec4 FragColor;
void main() { FragColor = vColor; }
";

    public GlViewportControl()
    {
        Focusable = true;
        DispatcherTimer.Run(() => { RequestNextFrameRendering(); return true; }, TimeSpan.FromMilliseconds(16));
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

        // Wall/Door/Slab/Stair/Room VAO/VBO/EBO
        _wallVao = GL.GenVertexArray(); _wallVbo = GL.GenBuffer(); _wallEbo = GL.GenBuffer();
        _doorVao = GL.GenVertexArray(); _doorVbo = GL.GenBuffer(); _doorEbo = GL.GenBuffer();
        _slabVao = GL.GenVertexArray(); _slabVbo = GL.GenBuffer(); _slabEbo = GL.GenBuffer();
        _stairVao = GL.GenVertexArray(); _stairVbo = GL.GenBuffer(); _stairEbo = GL.GenBuffer();
        _roomVao = GL.GenVertexArray(); _roomVbo = GL.GenBuffer(); _roomEbo = GL.GenBuffer();
        _roomOutlineVao = GL.GenVertexArray(); _roomOutlineVbo = GL.GenBuffer();
        _bgVao = GL.GenVertexArray(); _bgVbo = GL.GenBuffer(); _bgEbo = GL.GenBuffer();
        _bgOutlineVao = GL.GenVertexArray(); _bgOutlineVbo = GL.GenBuffer();

        // Ghost / Selection / Handle
        _ghostVao = GL.GenVertexArray(); _ghostVbo = GL.GenBuffer(); _ghostEbo = GL.GenBuffer();
        _outlineVao = GL.GenVertexArray(); _outlineVbo = GL.GenBuffer();
        _handleVao = GL.GenVertexArray(); _handleVbo = GL.GenBuffer(); _handleEbo = GL.GenBuffer();

        _initialized = true;
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (!_initialized) return;
        foreach (var buf in new[] { _gridVbo, _axisVbo, _wallVbo, _wallEbo, _doorVbo, _doorEbo,
            _slabVbo, _slabEbo, _stairVbo, _stairEbo, _roomVbo, _roomEbo, _roomOutlineVbo,
            _bgVbo, _bgEbo, _bgOutlineVbo,
            _ghostVbo, _ghostEbo, _outlineVbo, _handleVbo, _handleEbo })
            GL.DeleteBuffer(buf);
        foreach (var vao in new[] { _gridVao, _axisVao, _wallVao, _doorVao, _slabVao, _stairVao,
            _roomVao, _roomOutlineVao, _bgVao, _bgOutlineVao,
            _ghostVao, _outlineVao, _handleVao })
            GL.DeleteVertexArray(vao);
        GL.DeleteProgram(_shaderProgram);
        _initialized = false;
    }

    // ===================== Rendering =====================

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_initialized) return;

        var size = Bounds.Size;
        int w = (int)size.Width, h = (int)size.Height;
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

        // 3. Objects
        if (Editor != null)
        {
            RenderBackgrounds();
            RenderWalls();
            RenderDoors();
            RenderSlabs();
            RenderStairs();
            RenderRooms();
            RenderGhost();
            RenderSelection();
        }

        GL.BindVertexArray(0);
    }

    private void RenderBackgrounds()
    {
        if (Editor == null || !Editor.ShowBackgrounds) return;
        var bgs = Editor.VisibleBackgrounds.ToList();
        if (bgs.Count == 0) { _bgIndexCount = 0; return; }

        if (Editor.BackgroundsDirty || _bgIndexCount == 0)
        {
            var (v, i) = BackgroundRenderer.GenerateBatchedGeometry(bgs);
            _bgIndexCount = i.Length;
            if (v.Length > 0) UploadBuffer(_bgVao, _bgVbo, _bgEbo, v, i);
            Editor.BackgroundsDirty = false;
        }

        if (_bgIndexCount > 0)
        {
            GL.BindVertexArray(_bgVao);
            GL.DrawElements(PrimitiveType.Triangles, _bgIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderWalls()
    {
        if (Editor == null) return;
        var walls = Editor.VisibleWalls.ToList();
        if (walls.Count == 0) { _wallIndexCount = 0; return; }

        if (Editor.WallsDirty || _wallIndexCount == 0)
        {
            var (v, i) = WallRenderer.GenerateBatchedGeometry(walls);
            _wallIndexCount = i.Length;
            UploadBuffer(_wallVao, _wallVbo, _wallEbo, v, i);
            Editor.WallsDirty = false;
        }

        if (_wallIndexCount > 0)
        {
            GL.BindVertexArray(_wallVao);
            GL.DrawElements(PrimitiveType.Triangles, _wallIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderDoors()
    {
        if (Editor == null) return;
        var doors = Editor.VisibleDoors.ToList();
        if (doors.Count == 0) { _doorIndexCount = 0; return; }

        if (Editor.DoorsDirty || _doorIndexCount == 0)
        {
            var (v, i) = DoorRenderer.GenerateBatchedGeometry(doors, Editor.FindWallByName);
            _doorIndexCount = i.Length;
            if (v.Length > 0) UploadBuffer(_doorVao, _doorVbo, _doorEbo, v, i);
            Editor.DoorsDirty = false;
        }

        if (_doorIndexCount > 0)
        {
            GL.BindVertexArray(_doorVao);
            GL.DrawElements(PrimitiveType.Triangles, _doorIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderSlabs()
    {
        if (Editor == null) return;
        var slabs = Editor.VisibleSlabs.ToList();
        if (slabs.Count == 0) { _slabIndexCount = 0; return; }

        if (Editor.SlabsDirty || _slabIndexCount == 0)
        {
            var (v, i) = SlabRenderer.GenerateBatchedGeometry(slabs);
            _slabIndexCount = i.Length;
            if (v.Length > 0) UploadBuffer(_slabVao, _slabVbo, _slabEbo, v, i);
            Editor.SlabsDirty = false;
        }

        if (_slabIndexCount > 0)
        {
            GL.BindVertexArray(_slabVao);
            GL.DrawElements(PrimitiveType.Triangles, _slabIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderStairs()
    {
        if (Editor == null) return;
        var stairs = Editor.VisibleStairs.ToList();
        if (stairs.Count == 0) { _stairIndexCount = 0; return; }

        if (Editor.StairsDirty || _stairIndexCount == 0)
        {
            var (v, i) = StairRenderer.GenerateBatchedGeometry(stairs);
            _stairIndexCount = i.Length;
            if (v.Length > 0) UploadBuffer(_stairVao, _stairVbo, _stairEbo, v, i);
            Editor.StairsDirty = false;
        }

        if (_stairIndexCount > 0)
        {
            GL.BindVertexArray(_stairVao);
            GL.DrawElements(PrimitiveType.Triangles, _stairIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderRooms()
    {
        if (Editor == null) return;
        var rooms = Editor.VisibleRooms.ToList();
        if (rooms.Count == 0) { _roomIndexCount = 0; return; }

        if (Editor.RoomsDirty || _roomIndexCount == 0)
        {
            var (v, i) = RoomRenderer.GenerateBatchedGeometry(rooms);
            _roomIndexCount = i.Length;
            if (v.Length > 0) UploadBuffer(_roomVao, _roomVbo, _roomEbo, v, i);
            Editor.RoomsDirty = false;
        }

        if (_roomIndexCount > 0)
        {
            GL.BindVertexArray(_roomVao);
            GL.DrawElements(PrimitiveType.Triangles, _roomIndexCount, DrawElementsType.UnsignedShort, 0);
        }
    }

    private void RenderGhost()
    {
        if (Editor == null || !Editor.GhostRenderer.HasGhost) return;

        var (verts, indices) = Editor.GhostRenderer.GenerateGhostGeometry();
        if (verts == null || indices == null) return;

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
        if (Editor == null) return;

        // Wall selection
        foreach (var wall in Editor.SelectedWalls)
        {
            RenderWallSelection(wall);
        }

        // Door selection
        foreach (var door in Editor.SelectedDoors)
        {
            var wall = Editor.FindWallByName(door.ParentWallId);
            if (wall == null) continue;
            var outlineVerts = DoorRenderer.GenerateOutlineVertices(door, wall);
            RenderOutlineAndHandles(outlineVerts);
        }

        // Slab selection
        foreach (var slab in Editor.SelectedSlabs)
        {
            var outlineVerts = SlabRenderer.GenerateOutlineVertices(slab);
            if (outlineVerts.Length > 0) RenderOutlineAndHandles(outlineVerts);
        }

        // Stair selection
        foreach (var stair in Editor.SelectedStairs)
        {
            var outlineVerts = StairRenderer.GenerateOutlineVertices(stair);
            if (outlineVerts.Length > 0) RenderOutlineAndHandles(outlineVerts);
        }

        // Room selection
        foreach (var room in Editor.SelectedRooms)
        {
            var outlineVerts = RoomRenderer.GenerateOutlineVertices(room);
            if (outlineVerts.Length > 0)
            {
                var edgeIndices = RoomRenderer.GenerateOutlineEdgeIndices(room.Outline.Points.Count);
                GL.BindVertexArray(_roomOutlineVao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _roomOutlineVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, outlineVerts.Length * sizeof(float), outlineVerts, BufferUsageHint.DynamicDraw);
                SetupVertexAttribs();
                GL.LineWidth(2.0f);
                GL.DrawElements(PrimitiveType.Lines, edgeIndices.Length, DrawElementsType.UnsignedShort, 0);
                GL.LineWidth(1.0f);
                GL.BindVertexArray(0);
            }
        }

        // Background selection
        foreach (var bg in Editor.SelectedBackgrounds)
        {
            var floor = Editor.FindFloorForBackground(bg);
            if (floor == null) continue;
            var outlineVerts = BackgroundRenderer.GenerateOutlineVertices(bg, floor.Level);
            if (outlineVerts.Length > 0)
            {
                var edgeIndices = BackgroundRenderer.GetOutlineLineIndices();
                GL.BindVertexArray(_bgOutlineVao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _bgOutlineVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, outlineVerts.Length * sizeof(float), outlineVerts, BufferUsageHint.DynamicDraw);
                SetupVertexAttribs();
                GL.LineWidth(2.0f);
                GL.DrawElements(PrimitiveType.Lines, edgeIndices.Length, DrawElementsType.UnsignedShort, 0);
                GL.LineWidth(1.0f);
                GL.BindVertexArray(0);
            }
        }
    }

    private void RenderWallSelection(Wall wall)
    {
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

    private void RenderOutlineAndHandles(float[] outlineVerts)
    {
        var edgeIndices = WallRenderer.GetBoxEdgeIndices();
        GL.BindVertexArray(_outlineVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _outlineVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, outlineVerts.Length * sizeof(float), outlineVerts, BufferUsageHint.DynamicDraw);
        SetupVertexAttribs();
        GL.LineWidth(2.0f);
        GL.DrawElements(PrimitiveType.Lines, edgeIndices.Length, DrawElementsType.UnsignedShort, 0);
        GL.LineWidth(1.0f);
        GL.BindVertexArray(0);
    }

    private void UploadBuffer(int vao, int vbo, int ebo, float[] vertices, ushort[] indices)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        SetupVertexAttribs();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(ushort), indices, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
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

        if (!_isDragging && _isLeftDown)
        {
            var totalDelta = pos - _mouseDownPos;
            if (Math.Sqrt(totalDelta.X * totalDelta.X + totalDelta.Y * totalDelta.Y) > DragThreshold)
                _isDragging = true;
        }

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

        if (_isLeftDown && _isDragging && Editor != null)
        {
            var groundPos = GetGroundPosition(pos);

            switch (Editor.CurrentTool)
            {
                case ToolMode.Wall:
                    Editor.UpdateWallDraw(SnapPosition(groundPos));
                    break;
                case ToolMode.Slab:
                    Editor.UpdateSlabDraw(SnapPosition(groundPos));
                    break;
                case ToolMode.Stair:
                    Editor.UpdateStairDraw(SnapPosition(groundPos));
                    break;
                case ToolMode.Room:
                    Editor.UpdateRoomGhost(SnapPosition(groundPos));
                    break;
                case ToolMode.Select when Editor.ActiveTransform == TransformMode.Move:
                    Editor.UpdateMove(SnapPosition(groundPos));
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
                if (Editor.ActiveTransform == TransformMode.Move) Editor.ConfirmMove();
                else if (Editor.ActiveTransform == TransformMode.Rotate) Editor.ConfirmRotate();
            }
            else if (!_isDragging)
            {
                switch (Editor.CurrentTool)
                {
                    case ToolMode.Wall:
                        Editor.BeginWallDraw(SnapPosition(groundPos));
                        break;
                    case ToolMode.Door:
                        HandleDoorClick(pos);
                        break;
                    case ToolMode.Slab:
                        Editor.BeginSlabDraw(SnapPosition(groundPos));
                        break;
                    case ToolMode.Stair:
                        Editor.BeginStairDraw(SnapPosition(groundPos));
                        break;
                    case ToolMode.Room:
                        Editor.AddRoomPoint(SnapPosition(groundPos));
                        break;
                    case ToolMode.Select:
                        HandleSelectionClick(pos, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                        break;
                }
            }
            else if (_isDragging)
            {
                switch (Editor.CurrentTool)
                {
                    case ToolMode.Wall:
                        Editor.FinishWallDraw(SnapPosition(groundPos));
                        break;
                    case ToolMode.Slab:
                        Editor.FinishSlabDraw(SnapPosition(groundPos));
                        break;
                    case ToolMode.Stair:
                        Editor.FinishStairDraw(SnapPosition(groundPos));
                        break;
                }
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

        switch (e.Key)
        {
            case Avalonia.Input.Key.NumPad7: Camera.SetTopView(); return;
            case Avalonia.Input.Key.NumPad1: Camera.SetFrontView(); return;
            case Avalonia.Input.Key.NumPad3: Camera.SetRightView(); return;
            case Avalonia.Input.Key.NumPad5: Camera.ToggleProjection(); return;
            case Avalonia.Input.Key.Home: Camera.FitAll(10.0f); return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (e.Key == Avalonia.Input.Key.Z)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Editor.Redo(); else Editor.Undo();
                return;
            }
            if (e.Key == Avalonia.Input.Key.A)
            {
                Editor.ShowAllFloors();
                return;
            }
        }

        switch (e.Key)
        {
            case Avalonia.Input.Key.Escape:
                if (Editor.ActiveTransform != TransformMode.None)
                {
                    if (Editor.ActiveTransform == TransformMode.Move) Editor.CancelMove();
                    else if (Editor.ActiveTransform == TransformMode.Rotate) Editor.CancelRotate();
                }
                else if (Editor.CurrentTool == ToolMode.Room && Editor.RoomDrawPoints.Count > 0)
                    Editor.CancelRoomDraw();
                else if (Editor.CurrentTool != ToolMode.Select)
                    Editor.SetTool(ToolMode.Select);
                else
                    Editor.ClearSelection();
                break;
            case Avalonia.Input.Key.Return:
                if (Editor.CurrentTool == ToolMode.Room && Editor.RoomDrawPoints.Count >= 3)
                {
                    Editor.FinishRoomDraw();
                    Editor.SetTool(ToolMode.Select);
                }
                break;
            case Avalonia.Input.Key.W: Editor.SetTool(ToolMode.Wall); break;
            case Avalonia.Input.Key.D: Editor.SetTool(ToolMode.Door); break;
            case Avalonia.Input.Key.P: Editor.SetTool(ToolMode.Slab); break;
            case Avalonia.Input.Key.L: Editor.SetTool(ToolMode.Stair); break;
            case Avalonia.Input.Key.M: Editor.SetTool(ToolMode.Room); break;
            case Avalonia.Input.Key.G: StartMove(); break;
            case Avalonia.Input.Key.R:
                if (Editor.ActiveTransform == TransformMode.Rotate) Editor.ConfirmRotate();
                else StartRotate();
                break;
            case Avalonia.Input.Key.X:
            case Avalonia.Input.Key.Delete: Editor.DeleteSelected(); break;
            case Avalonia.Input.Key.F4: Editor.SnapEngine.IsEnabled = !Editor.SnapEngine.IsEnabled; break;
            case Avalonia.Input.Key.OemOpenBrackets: Editor.SnapEngine.DecreaseGridStep(); break;
            case Avalonia.Input.Key.OemCloseBrackets: Editor.SnapEngine.IncreaseGridStep(); break;
        }
    }

    // ===================== Public API =====================

    public void StartMove()
    {
        if (Editor == null || !Editor.HasAnySelection) return;

        if (Editor.SingleSelectedWall is { } wall)
        {
            Editor.BeginMove(new Point2D((wall.Start.X + wall.End.X) / 2, (wall.Start.Y + wall.End.Y) / 2));
        }
        else if (Editor.SingleSelectedDoor is { } door)
        {
            Editor.BeginMove(door.Position);
        }
        else if (Editor.SingleSelectedSlab is { } slab && slab.Outline.Points.Count >= 4)
        {
            var cx = slab.Outline.Points.Average(p => p.X);
            var cy = slab.Outline.Points.Average(p => p.Y);
            Editor.BeginMove(new Point2D(cx, cy));
        }
        else if (Editor.SingleSelectedStair is { } stair)
        {
            Editor.BeginMove(new Point2D((stair.Start.X + stair.End.X) / 2, (stair.Start.Y + stair.End.Y) / 2));
        }
        else if (Editor.SingleSelectedRoom is { } room && room.Outline.Points.Count >= 3)
        {
            var cx = room.Outline.Points.Average(p => p.X);
            var cy = room.Outline.Points.Average(p => p.Y);
            Editor.BeginMove(new Point2D(cx, cy));
        }
        else if (Editor.SingleSelectedBackground is { } bg)
        {
            Editor.BeginMove(new Point2D(bg.Origin.X + bg.RealWidth / 2, bg.Origin.Y + bg.RealHeight / 2));
        }
    }

    public void StartRotate()
    {
        if (Editor?.SingleSelectedWall is not { } wall) return;
        var center = new Point2D((wall.Start.X + wall.End.X) / 2, (wall.Start.Y + wall.End.Y) / 2);
        Editor.BeginRotate(new Point2D(center.X + 1.0, center.Y));
    }

    // ===================== Helpers =====================

    private Point2D GetGroundPosition(Point screenPos)
    {
        var size = Bounds.Size;
        int w = (int)size.Width, h = (int)size.Height;
        if (w <= 0 || h <= 0) return new Point2D(0, 0);
        double groundZ = Editor?.ActiveFloor.Level ?? 0.0;
        return ObjectPicker.ScreenToGroundPlane(screenPos.X, screenPos.Y, w, h, Camera.GetViewMatrix(), Camera.GetProjectionMatrix(), groundZ);
    }

    private Point2D SnapPosition(Point2D worldPos)
    {
        if (Editor == null) return worldPos;
        return Editor.SnapEngine.Snap(worldPos, Editor.ActiveWalls).Position;
    }

    private void HandleDoorClick(Point screenPos)
    {
        if (Editor == null) return;
        var size = Bounds.Size;
        int w = (int)size.Width, h = (int)size.Height;
        if (w <= 0 || h <= 0) return;

        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();

        var groundPos = GetGroundPosition(screenPos);

        // Find nearest wall across visible floors and place door on it
        var wall = ObjectPicker.PickWall(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleWalls);
        if (wall != null)
            Editor.PlaceDoor(wall, groundPos);
    }

    private void HandleSelectionClick(Point screenPos, bool addToSelection)
    {
        if (Editor == null) return;
        var size = Bounds.Size;
        int w = (int)size.Width, h = (int)size.Height;
        if (w <= 0 || h <= 0) return;

        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();

        // Try picking in priority order: door > stair > slab > room > wall > background
        var door = ObjectPicker.PickDoor(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleDoors, Editor.FindWallByName);
        if (door != null) { Editor.SelectDoor(door, addToSelection); return; }

        var stair = ObjectPicker.PickStair(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleStairs);
        if (stair != null) { Editor.SelectStair(stair, addToSelection); return; }

        var slab = ObjectPicker.PickSlab(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleSlabs);
        if (slab != null) { Editor.SelectSlab(slab, addToSelection); return; }

        var room = ObjectPicker.PickRoom(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleRooms);
        if (room != null) { Editor.SelectRoom(room, addToSelection); return; }

        var wall = ObjectPicker.PickWall(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleWalls);
        if (wall != null) { Editor.SelectWall(wall, addToSelection); return; }

        // Lowest priority: background
        var bg = ObjectPicker.PickBackground(screenPos.X, screenPos.Y, w, h, view, proj, Editor.VisibleBackgrounds);
        Editor.SelectBackground(bg, addToSelection);
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

internal class AvaloniaGlBindings : OpenTK.IBindingsContext
{
    private readonly GlInterface _gl;
    public AvaloniaGlBindings(GlInterface gl) => _gl = gl;
    public IntPtr GetProcAddress(string procName) => _gl.GetProcAddress(procName);
}
