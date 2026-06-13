using OpenTK.Mathematics;

namespace FdsGui.Viewport;

/// <summary>
/// Orbit camera with Blender-style controls.
/// Orbits around a target point, supports pan and zoom.
/// </summary>
public class Camera
{
    // Orbit parameters
    public Vector3 Target { get; set; } = Vector3.Zero;
    public float Distance { get; set; } = 20.0f;
    public float Azimuth { get; set; } = MathHelper.DegreesToRadians(45);
    public float Elevation { get; set; } = MathHelper.DegreesToRadians(35);

    // Projection
    public bool IsPerspective { get; set; } = true;
    public float FovDegrees { get; set; } = 45.0f;
    public float OrthoSize { get; set; } = 20.0f;
    public float NearPlane { get; set; } = 0.01f;
    public float FarPlane { get; set; } = 10000.0f;
    public float AspectRatio { get; set; } = 16.0f / 9.0f;

    // Speed settings
    public float OrbitSpeed { get; set; } = 0.005f;
    public float PanSpeed { get; set; } = 0.02f;
    public float ZoomSpeed { get; set; } = 1.1f;
    public float MinDistance { get; set; } = 0.5f;
    public float MaxDistance { get; set; } = 5000.0f;

    public Vector3 Position
    {
        get
        {
            float cosElev = MathF.Cos(Elevation);
            float sinElev = MathF.Sin(Elevation);
            float cosAzim = MathF.Cos(Azimuth);
            float sinAzim = MathF.Sin(Azimuth);

            var offset = new Vector3(
                Distance * cosElev * sinAzim,
                Distance * cosElev * cosAzim,
                Distance * sinElev
            );

            return Target + offset;
        }
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Target, Vector3.UnitZ);
    }

    public Matrix4 GetProjectionMatrix()
    {
        if (IsPerspective)
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(FovDegrees),
                AspectRatio,
                NearPlane,
                FarPlane
            );
        }
        else
        {
            float halfH = OrthoSize / 2.0f;
            float halfW = halfH * AspectRatio;
            return Matrix4.CreateOrthographicOffCenter(
                -halfW, halfW, -halfH, halfH,
                NearPlane, FarPlane
            );
        }
    }

    // --- Input handlers ---

    public void Orbit(float deltaX, float deltaY)
    {
        Azimuth -= deltaX * OrbitSpeed;
        Elevation += deltaY * OrbitSpeed;

        // Clamp elevation to avoid gimbal issues
        float limit = MathHelper.DegreesToRadians(89.0f);
        Elevation = Math.Clamp(Elevation, -limit, limit);
    }

    public void Pan(float deltaX, float deltaY)
    {
        var view = GetViewMatrix();
        // Extract camera right and up vectors from view matrix
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);

        float scale = Distance * PanSpeed * 0.01f;
        Target -= right * deltaX * scale;
        Target += up * deltaY * scale;
    }

    public void Zoom(float delta)
    {
        if (delta > 0)
            Distance /= ZoomSpeed;
        else
            Distance *= ZoomSpeed;

        Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
    }

    // --- Preset views ---

    public void SetTopView()
    {
        Elevation = MathHelper.DegreesToRadians(89.0f);
        Azimuth = 0;
    }

    public void SetFrontView()
    {
        Elevation = 0;
        Azimuth = 0;
    }

    public void SetRightView()
    {
        Elevation = 0;
        Azimuth = MathHelper.DegreesToRadians(90);
    }

    public void SetIsoView()
    {
        Elevation = MathHelper.DegreesToRadians(35);
        Azimuth = MathHelper.DegreesToRadians(45);
    }

    public void ToggleProjection()
    {
        IsPerspective = !IsPerspective;
    }

    /// Fit all: adjust distance to fit a bounding sphere of given radius
    public void FitAll(float boundingRadius)
    {
        if (boundingRadius < 0.1f) boundingRadius = 5.0f;

        if (IsPerspective)
        {
            float halfFov = MathHelper.DegreesToRadians(FovDegrees / 2.0f);
            Distance = boundingRadius / MathF.Sin(halfFov);
        }
        else
        {
            OrthoSize = boundingRadius * 2.5f;
        }

        Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
    }
}
