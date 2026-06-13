using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using FdsGui.Viewport;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace FdsGui.App.Controls;

/// <summary>
/// OpenGL viewport control with Blender-style camera and grid rendering.
/// Uses OpenTK.Graphics.OpenGL4 for GL calls, with Avalonia providing the context.
/// </summary>
public class GlViewportControl : OpenGlControlBase
{
    public Camera Camera { get; } = new();
    private readonly GridRenderer _grid = new();

    // OpenGL resources
    private int _gridVao;
    private int _gridVbo;
    private int _gridVertexCount;
    private int _axisVao;
    private int _axisVbo;
    private int _shaderProgram;
    private int _uMvp;
    private bool _initialized;

    // Mouse state
    private Point _lastMouse;
    private bool _isMiddleDown;

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

    protected override void OnOpenGlInit(GlInterface gl)
    {
        // Load OpenTK bindings using Avalonia's GL function resolver
        var bindings = new AvaloniaGlBindings(gl);
        GL.LoadBindings(bindings);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1.0f);

        // Compile shaders
        int vs = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        int fs = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vs);
        GL.AttachShader(_shaderProgram, fs);
        GL.LinkProgram(_shaderProgram);

        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string log = GL.GetProgramInfoLog(_shaderProgram);
            System.Diagnostics.Debug.WriteLine($"Shader link error: {log}");
        }

        GL.DetachShader(_shaderProgram, vs);
        GL.DetachShader(_shaderProgram, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        _uMvp = GL.GetUniformLocation(_shaderProgram, "uMVP");

        // Setup grid
        float[] gridVerts = _grid.GenerateGridVertices();
        _gridVertexCount = gridVerts.Length / 7;

        _gridVao = GL.GenVertexArray();
        GL.BindVertexArray(_gridVao);
        _gridVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _gridVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, gridVerts.Length * sizeof(float), gridVerts, BufferUsageHint.StaticDraw);

        // Position (location 0): 3 floats, stride 7*4, offset 0
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        // Color (location 1): 4 floats, stride 7*4, offset 3*4
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);

        // Setup axes
        float[] axisVerts = _grid.GenerateAxisVertices();

        _axisVao = GL.GenVertexArray();
        GL.BindVertexArray(_axisVao);
        _axisVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _axisVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, axisVerts.Length * sizeof(float), axisVerts, BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);

        _initialized = true;
    }

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

        // MVP
        Matrix4 view = Camera.GetViewMatrix();
        Matrix4 proj = Camera.GetProjectionMatrix();
        Matrix4 mvp = view * proj;
        GL.UniformMatrix4(_uMvp, false, ref mvp);

        // Draw grid
        GL.BindVertexArray(_gridVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _gridVertexCount);

        // Draw axes (thicker)
        GL.LineWidth(2.5f);
        GL.BindVertexArray(_axisVao);
        GL.DrawArrays(PrimitiveType.Lines, 0, GridRenderer.AxisVertexCount);
        GL.LineWidth(1.0f);

        GL.BindVertexArray(0);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (!_initialized) return;
        GL.DeleteBuffer(_gridVbo);
        GL.DeleteVertexArray(_gridVao);
        GL.DeleteBuffer(_axisVbo);
        GL.DeleteVertexArray(_axisVao);
        GL.DeleteProgram(_shaderProgram);
        _initialized = false;
    }

    // --- Input handling (Blender-style camera) ---

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        _lastMouse = e.GetPosition(this);
        _isMiddleDown = e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        var delta = pos - _lastMouse;

        if (_isMiddleDown)
        {
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (shift)
                Camera.Pan((float)delta.X, (float)delta.Y);
            else
                Camera.Orbit((float)delta.X, (float)delta.Y);
        }
        else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            Camera.Pan((float)delta.X, (float)delta.Y);
        }

        _lastMouse = pos;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isMiddleDown = false;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Camera.Zoom((float)e.Delta.Y);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Avalonia.Input.Key.NumPad7: Camera.SetTopView(); break;
            case Avalonia.Input.Key.NumPad1: Camera.SetFrontView(); break;
            case Avalonia.Input.Key.NumPad3: Camera.SetRightView(); break;
            case Avalonia.Input.Key.NumPad5: Camera.ToggleProjection(); break;
            case Avalonia.Input.Key.Home: Camera.FitAll(10.0f); break;
        }
    }

    // --- Helpers ---

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            System.Diagnostics.Debug.WriteLine($"Shader compile error ({type}): {log}");
        }

        return shader;
    }
}

/// <summary>
/// Bridges Avalonia's GlInterface to OpenTK's IBindingsContext.
/// </summary>
internal class AvaloniaGlBindings : OpenTK.IBindingsContext
{
    private readonly GlInterface _gl;

    public AvaloniaGlBindings(GlInterface gl)
    {
        _gl = gl;
    }

    public IntPtr GetProcAddress(string procName)
    {
        return _gl.GetProcAddress(procName);
    }
}
