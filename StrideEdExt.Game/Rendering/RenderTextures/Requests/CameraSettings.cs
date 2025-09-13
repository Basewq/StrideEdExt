using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;

namespace StrideEdExt.Rendering.RenderTextures.Requests;

public struct CameraSettings
{
    public CameraProjectionMode Projection;
    public float VerticalFieldOfView;
    public float OrthographicSize;
    public float NearClipPlane;
    public float FarClipPlane;
    public float? CustomAspectRatio;
    public Matrix? CustomViewMatrix;
    public Matrix? CustomProjectionMatrix;

    public static CameraSettings FromCameraComponent(CameraComponent cameraComponent)
    {
        var camSettings = new CameraSettings
        {
            Projection = cameraComponent.Projection,
            VerticalFieldOfView = cameraComponent.VerticalFieldOfView,
            OrthographicSize = cameraComponent.OrthographicSize,
            NearClipPlane = cameraComponent.NearClipPlane,
            FarClipPlane = cameraComponent.FarClipPlane,
            CustomAspectRatio = cameraComponent.UseCustomAspectRatio ? cameraComponent.AspectRatio : null,
            CustomViewMatrix = cameraComponent.UseCustomViewMatrix ? cameraComponent.ViewMatrix : null,
            CustomProjectionMatrix = cameraComponent.UseCustomProjectionMatrix ? cameraComponent.ProjectionMatrix : null,
        };
        return camSettings;
    }

    public void ToCameraComponent(CameraComponent cameraComponent)
    {

        cameraComponent.Projection = Projection;
        cameraComponent.VerticalFieldOfView = VerticalFieldOfView;
        cameraComponent.OrthographicSize = OrthographicSize;
        cameraComponent.NearClipPlane = NearClipPlane;
        cameraComponent.FarClipPlane = FarClipPlane;
        if (CustomAspectRatio is float customAspectRatio)
        {
            cameraComponent.UseCustomAspectRatio = true;
            cameraComponent.AspectRatio = customAspectRatio;
        }
        else
        {
            cameraComponent.UseCustomAspectRatio = false;
        }
        if (CustomViewMatrix is Matrix customViewMatrix)
        {
            cameraComponent.UseCustomViewMatrix = true;
            cameraComponent.ViewMatrix = customViewMatrix;
        }
        else
        {
            cameraComponent.UseCustomViewMatrix = false;
        }
        if (CustomProjectionMatrix is Matrix customProjectionMatrix)
        {
            cameraComponent.UseCustomProjectionMatrix = true;
            cameraComponent.ProjectionMatrix = customProjectionMatrix;
        }
        else
        {
            cameraComponent.UseCustomProjectionMatrix = false;
        }
    }
}