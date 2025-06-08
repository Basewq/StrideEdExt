using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using Stride.Rendering.Materials;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SceneEditorExtensionExample.Rendering.RenderTextures.Requests;

public class RenderHeightmapTextureRequest : IRenderTextureRequest
{
    private const string GraphicsCompositorKey = "RenderHeightmapTextureKey";

    private Int2 _texturePixelStartPosition;
    private Texture? _renderTargetTexture;

    public readonly Model Model;
    public readonly TransformTRS TransformTRS;
    public readonly Vector2 TextureOriginWorldPosition;
    public readonly Vector2 UnitsPerPixel;

    public RenderHeightmapTextureRequest(Model model, TransformTRS transformTRS, Vector2 textureOriginWorldPosition, Vector2 unitsPerPixel)
    {
        Model = model;
        TransformTRS = transformTRS;
        TextureOriginWorldPosition = textureOriginWorldPosition;
        UnitsPerPixel = unitsPerPixel;
    }

    public void SetUpScene(SceneSystem sceneSystem, GraphicsDevice graphicsDevice)
    {
        // Create the model render scene
        var entityScene = new Scene();

        // Create the model entity
        var modelEntity = new Entity { Name = "Render Heightmap Model" };
        modelEntity.Transform.Position = TransformTRS.Position;
        modelEntity.Transform.Rotation = TransformTRS.Rotation;
        modelEntity.Transform.Scale = TransformTRS.Scale;
        var entityBoundingBox = (BoundingBox)new BoundingBoxExt(Model.BoundingBox.Center, Model.BoundingBox.Extent * TransformTRS.Scale);
        BoundingSphere.FromBox(ref entityBoundingBox, out var entityBoundingSphere);
        var modelComp = new ModelComponent
        {
            Model = Model,
            BoundingBox = entityBoundingBox,
            BoundingSphere = entityBoundingSphere
        };
        modelEntity.Add(modelComp);
        entityScene.Entities.Add(modelEntity);

        var texWorldMeasurement = new TextureWorldMeasurement(TextureOriginWorldPosition, UnitsPerPixel);

        // Determine the required render texture
        var modelBoundingBoxExt = CalculateBoundBoxExt(modelEntity);
        var texRegion = texWorldMeasurement.GetTextureCoordsRegionXZ(modelBoundingBoxExt.Minimum, modelBoundingBoxExt.Maximum); 

        // Note this texture uses the G channel as the mask channel
        _renderTargetTexture = Texture.New2D(
            graphicsDevice,
            texRegion.Width, texRegion.Height,
            PixelFormat.R16G16_Float, TextureFlags.ShaderResource | TextureFlags.RenderTarget,
            usage: GraphicsResourceUsage.Default);

        var graphicsCompositor = graphicsDevice.GetOrCreateSharedData(GraphicsCompositorKey, gfxDevice => CreateSharedGraphicsCompositor());
        SetRenderTexture(_renderTargetTexture, graphicsCompositor);
        sceneSystem.GraphicsCompositor = graphicsCompositor;

        // The texture starting position relative to the 'world position' (in pixel space)
        _texturePixelStartPosition = new Int2(texRegion.Left, texRegion.Top);

        // Create the camera entity
        // Get the pixel coordinates.
        var topLeft = new Int2(texRegion.Left, texRegion.Top);
        var bottomRight = new Int2(texRegion.Right, texRegion.Bottom);
        var textureWorldRegion = texWorldMeasurement.GetWorldViewRegionXZ(topLeft, bottomRight);
        var cameraEntity = CreateCameraEntity(textureWorldRegion, modelBoundingBoxExt, graphicsCompositor);
        entityScene.Entities.Add(cameraEntity);

        sceneSystem.SceneInstance.RootScene.Children.Add(entityScene);
    }

    public void RenderCompleted(SceneSystem sceneSystem, GraphicsDevice graphicsDevice, RenderTextureResult result)
    {
        sceneSystem.SceneInstance.RootScene.Children.Clear();
        SetRenderTexture(renderTargetTexture: null, sceneSystem.GraphicsCompositor);
        sceneSystem.GraphicsCompositor = null;

        Debug.Assert(_renderTargetTexture is not null);
        result.Texture = _renderTargetTexture;
        result.TexturePixelStartPosition = _texturePixelStartPosition;
        result.State = RenderTextureResultStateType.Success;
    }

    private static void SetRenderTexture(Texture? renderTargetTexture, GraphicsCompositor graphicsCompositor)
    {
        if (graphicsCompositor.Game is SceneCameraRenderer cameraRenderer
            && cameraRenderer.Child is RenderTextureSceneRenderer rendTexSceneRenderer)
        {
            rendTexSceneRenderer.RenderTexture = renderTargetTexture;
        }
        else
        {
            throw new InvalidOperationException("GraphicsCompositor not setup correctly.");
        }
    }

    // Cutdown version from Stride.Rendering.Compositing.GraphicsCompositorHelper.CreateDefault
    private static GraphicsCompositor CreateSharedGraphicsCompositor()
    {
        const string RenderEffectName = "HeightmapRenderEffect";
        var clearColor = Color.Transparent;
        var groupMask = RenderGroupMask.All;

        var opaqueRenderStage = new RenderStage("Opaque", "Main") { SortMode = new StateChangeSortMode() };
        var transparentRenderStage = new RenderStage("Transparent", "Main") { SortMode = new BackToFrontSortMode() };
        var gBufferRenderStage = new RenderStage("GBuffer", "GBuffer") { SortMode = new FrontToBackSortMode() };

        var forwardRenderer = new ForwardRenderer
        {
            Clear = { Color = clearColor },
            OpaqueRenderStage = opaqueRenderStage,
            TransparentRenderStage = transparentRenderStage,
            GBufferRenderStage = gBufferRenderStage,
            LightProbes = false,
        };
        var renderTextureSceneRenderer = new RenderTextureSceneRenderer
        {
            Child = forwardRenderer
        };

        var cameraSlot = new SceneCameraSlot
        {
            Name = "RenderHeightmapCameraSlot",
        };

        return new GraphicsCompositor
        {
            Name = "RenderHeightmapGraphicsCompositor",
            Cameras = { cameraSlot, },
            RenderStages =
            {
                opaqueRenderStage,
                transparentRenderStage,
                gBufferRenderStage,
            },
            RenderFeatures =
            {
                new MeshRenderFeature
                {
                    RenderFeatures =
                    {
                        new TransformRenderFeature(),
                        new SkinningRenderFeature(),
                        new MaterialRenderFeature(),
                        new InstancingRenderFeature(),
                    },
                    RenderStageSelectors =
                    {
                        new MeshTransparentRenderStageSelector
                        {
                            EffectName = RenderEffectName,
                            OpaqueRenderStage = opaqueRenderStage,
                            TransparentRenderStage = transparentRenderStage,
                            RenderGroup = groupMask,
                        },
                        //new MeshTransparentRenderStageSelector
                        //{
                        //    EffectName = RenderEffectName,
                        //    OpaqueRenderStage = gBufferRenderStage,
                        //    //TransparentRenderStage = transparentRenderStage,
                        //    RenderGroup = groupMask,
                        //},
                    },
                    PipelineProcessors =
                    {
                        new MeshPipelineProcessor { TransparentRenderStage = transparentRenderStage },
                    },
                },
                //new SpriteRenderFeature
                //{
                //    RenderStageSelectors =
                //    {
                //        new SpriteTransparentRenderStageSelector
                //        {
                //            EffectName = "Test",
                //            OpaqueRenderStage = opaqueRenderStage,
                //            TransparentRenderStage = transparentRenderStage,
                //            RenderGroup = groupMask,
                //        },
                //    },
                //},
                //new BackgroundRenderFeature
                //{
                //    RenderStageSelectors =
                //    {
                //        new SimpleGroupToRenderStageSelector
                //        {
                //            RenderStage = opaqueRenderStage,
                //            EffectName = "Test",
                //            RenderGroup = groupMask,
                //        },
                //    },
                //},
            },
            Game = new SceneCameraRenderer()
            {
                Child = renderTextureSceneRenderer,
                Camera = cameraSlot,
            },
            Editor = renderTextureSceneRenderer,
            SingleView = renderTextureSceneRenderer,
        };
    }

    private static BoundingBoxExt CalculateBoundBoxExt(Entity entity, bool isRecursive = true, Func<Model, IEnumerable<Mesh>>? meshSelector = null)
    {
        // Adapted cutdown version of Stride.Editor.Engine.EntityExtensions.CalculateBoundSphere (also changed to return BoundingBoxExt)
        entity.Transform.UpdateWorldMatrix();
        var worldMatrix = entity.Transform.WorldMatrix;

        var totalModelBoundingBoxExt = new BoundingBoxExt
        {
            Center = worldMatrix.TranslationVector
        };

        // calculate the bounding sphere of the model if any
        var modelComponent = entity.Get<ModelComponent>();
        var model = modelComponent?.Model;
        if (model is not null)
        {
            var hierarchy = modelComponent!.Skeleton;
            var nodeTransforms = new Matrix[hierarchy.Nodes.Length];

            // Calculate node transforms here, since there might not be a ModelProcessor running
            for (int i = 0; i < nodeTransforms.Length; i++)
            {
                if (hierarchy.Nodes[i].ParentIndex == -1)
                {
                    nodeTransforms[i] = worldMatrix;
                }
                else
                {
                    Matrix.Transformation(
                        ref hierarchy.Nodes[i].Transform.Scale,
                        ref hierarchy.Nodes[i].Transform.Rotation,
                        ref hierarchy.Nodes[i].Transform.Position, out Matrix localMatrix);
                    Matrix.Multiply(ref localMatrix, ref nodeTransforms[hierarchy.Nodes[i].ParentIndex], out nodeTransforms[i]);
                }
            }

            // calculate the bounding box
            var totalMeshBoundingBox = new BoundingBoxExt
            {
                Center = worldMatrix.TranslationVector
            };

            var meshes = model.Meshes;
            var filteredMeshes = meshSelector == null ? meshes : meshSelector(model);

            // Calculate skinned bounding boxes.
            // TODO: Cloned from ModelSkinningUpdater. Consolidate.
            foreach (var mesh in filteredMeshes)
            {
                var skinning = mesh.Skinning;
                if (skinning is null)
                {
                    // For unskinned meshes, use the original bounding box
                    var meshBoundingBoxExt = (BoundingBoxExt)mesh.BoundingBox;
                    meshBoundingBoxExt.Transform(nodeTransforms[mesh.NodeIndex]);
                    BoundingBoxExt.Merge(ref totalMeshBoundingBox, ref meshBoundingBoxExt, out totalMeshBoundingBox);
                }
                else
                {
                    var bones = skinning.Bones;
                    var bindPoseBoundingBox = new BoundingBoxExt(mesh.BoundingBox);

                    for (int index = 0; index < bones.Length; index++)
                    {
                        var nodeIndex = bones[index].NodeIndex;
                        // Compute bone matrix
                        Matrix.Multiply(ref bones[index].LinkToMeshMatrix, ref nodeTransforms[nodeIndex], out Matrix boneMatrix);

                        // Fast AABB transform: http://zeuxcg.org/2010/10/17/aabb-from-obb-with-component-wise-abs/
                        // Compute transformed AABB (by world)
                        var boundingBoxExt = bindPoseBoundingBox;
                        boundingBoxExt.Transform(boneMatrix);
                        BoundingBoxExt.Merge(in totalMeshBoundingBox, in boundingBoxExt, out totalMeshBoundingBox);
                    }
                }
            }
            BoundingBoxExt.Merge(ref totalModelBoundingBoxExt, ref totalMeshBoundingBox, out totalModelBoundingBoxExt);
        }

        // Extend the bounding box to include the children
        if (isRecursive)
        {
            foreach (var child in entity.GetChildren())
            {
                var childBoundingBoxExt = CalculateBoundBoxExt(child, isRecursive: true, meshSelector);
                BoundingBoxExt.Merge(in totalModelBoundingBoxExt, in childBoundingBoxExt, out totalModelBoundingBoxExt);
            }
        }

        return totalModelBoundingBoxExt;
    }

    private static Entity CreateCameraEntity(in RectangleF textureWorldRegion, in BoundingBoxExt modelBoundingBoxExt, GraphicsCompositor graphicsCompositor)
    {
        var cameraSlot = graphicsCompositor.Cameras[0];
        var cameraComponent = new CameraComponent
        {
            Slot = cameraSlot.ToSlotId(),
            Projection = CameraProjectionMode.Orthographic,
            UseCustomAspectRatio = true,
            AspectRatio = textureWorldRegion.Width / textureWorldRegion.Height,
            OrthographicSize = textureWorldRegion.Height,
        };

        var camPosXZ = textureWorldRegion.Center;
        var camPosX = camPosXZ.X;
        var camPosZ = camPosXZ.Y;
        const float CameraHeightOffset = 10f;    // Provide some margin between the camera and model
        float minHeightPos = modelBoundingBoxExt.Center.Y - modelBoundingBoxExt.Extent.Y;
        float maxHeightPos = modelBoundingBoxExt.Center.Y + modelBoundingBoxExt.Extent.Y;

        cameraComponent.NearClipPlane = 0.1f;
        cameraComponent.FarClipPlane = (maxHeightPos - minHeightPos) + 2 * CameraHeightOffset;

        var cameraEntity = new Entity("Render Heightmap Camera") { cameraComponent };
        cameraEntity.Transform.Position = new Vector3(camPosX, maxHeightPos + CameraHeightOffset, camPosZ);
        cameraEntity.Transform.Rotation = Quaternion.RotationX(-MathUtil.PiOverTwo);     // Look straight down

        return cameraEntity;
    }
}
