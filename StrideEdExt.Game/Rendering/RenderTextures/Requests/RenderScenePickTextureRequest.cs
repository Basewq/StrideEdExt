//using Stride.Core.Mathematics;
//using Stride.Engine;
//using Stride.Engine.Processors;
//using Stride.Graphics;
//using Stride.Rendering;
//using Stride.Rendering.Compositing;
//using Stride.Rendering.Materials;
//using StrideEdExt.Rendering.PaintRenderer;
//using System.Diagnostics;

//namespace StrideEdExt.Rendering.RenderTextures.Requests;

//public class RenderScenePickTextureResult : RenderTextureResult
//{
//    public PaintingPickHitResultData PickHitResult;
//}

//public class RenderScenePickTextureRequest : IRenderTextureRequest<RenderScenePickTextureResult>
//{
//    private const string GraphicsCompositorKey = "RenderScenePickTextureKey";

//    private Texture? _renderTargetTexture;

//    public readonly Model Model;
//    public readonly TransformTRS ModelTransformTRS;

//    public readonly CameraSettings CameraSettings;
//    public readonly TransformTRS CameraTransformTRS;

//    public RenderScenePickTextureRequest(Model model, TransformTRS modelTransformTRS, CameraSettings cameraSettings, TransformTRS cameraTransformTRS)
//    {
//        Model = model;
//        ModelTransformTRS = modelTransformTRS;
//        CameraSettings = cameraSettings;
//        CameraTransformTRS = cameraTransformTRS;
//    }

//    public void SetUpScene(SceneSystem sceneSystem, GraphicsDevice graphicsDevice)
//    {
//        // Create the model render scene
//        var entityScene = new Scene();

//        // Create the model entity
//        var modelEntity = new Entity { Name = "Render Pick Model" };
//        ModelTransformTRS.SetToEntity(modelEntity);
//        var entityBoundingBox = (BoundingBox)new BoundingBoxExt(Model.BoundingBox.Center, Model.BoundingBox.Extent * ModelTransformTRS.Scale);
//        BoundingSphere.FromBox(ref entityBoundingBox, out var entityBoundingSphere);
//        var modelComp = new ModelComponent
//        {
//            Model = Model,
//            BoundingBox = entityBoundingBox,
//            BoundingSphere = entityBoundingSphere
//        };
//        modelEntity.Add(modelComp);
//        entityScene.Entities.Add(modelEntity);

//        // Determine the required render texture
//        var textureWidth = graphicsDevice.Presenter.BackBuffer.Width;
//        var textureHeight = graphicsDevice.Presenter.BackBuffer.Height;
//        // Note this texture uses the G channel as the mask channel
//        _renderTargetTexture = Texture.New2D(
//            graphicsDevice,
//            textureWidth, textureHeight,
//            PixelFormat.R8_UInt, TextureFlags.ShaderResource | TextureFlags.RenderTarget,
//            usage: GraphicsResourceUsage.Default);

//        var graphicsCompositor = graphicsDevice.GetOrCreateSharedData(GraphicsCompositorKey, gfxDevice => CreateSharedGraphicsCompositor());
//        SetRenderTexture(_renderTargetTexture, graphicsCompositor);
//        sceneSystem.GraphicsCompositor = graphicsCompositor;

//        // Create the camera entity
//        var cameraEntity = CreateCameraEntity(CameraSettings, CameraTransformTRS, graphicsCompositor);
//        entityScene.Entities.Add(cameraEntity);

//        sceneSystem.SceneInstance.RootScene.Children.Add(entityScene);
//    }

//    public RenderScenePickTextureResult RenderCompleted(SceneSystem sceneSystem, GraphicsDevice graphicsDevice)
//    {
//        sceneSystem.SceneInstance.RootScene.Children.Clear();
//        SetRenderTexture(renderTargetTexture: null, sceneSystem.GraphicsCompositor);
//        sceneSystem.GraphicsCompositor = null;

//        Debug.Assert(_renderTargetTexture is not null);
//        var result = new RenderScenePickTextureResult
//        {
//            PickHitResult = ???,    // TODO get from buffer
//            Texture = _renderTargetTexture,
//            TexturePixelStartPosition = Int2.Zero,
//            State = RenderTextureResultStateType.Success,
//        };
//        return result;
//    }

//    private static void SetRenderTexture(Texture? renderTargetTexture, GraphicsCompositor graphicsCompositor)
//    {
//        if (graphicsCompositor.Game is SceneCameraRenderer cameraRenderer
//            && cameraRenderer.Child is RenderTextureSceneRenderer rendTexSceneRenderer)
//        {
//            rendTexSceneRenderer.RenderTexture = renderTargetTexture!;
//        }
//        else
//        {
//            throw new InvalidOperationException("GraphicsCompositor not setup correctly.");
//        }
//    }

//    // Cutdown version from Stride.Rendering.Compositing.GraphicsCompositorHelper.CreateDefault
//    private static GraphicsCompositor CreateSharedGraphicsCompositor()
//    {
//        const string RenderEffectName = "PaintingPickOutputEffect";
//        var clearColor = Color.Transparent;
//        var groupMask = RenderGroupMask.All;

//        var opaqueRenderStage = new RenderStage("Opaque", "Main") { SortMode = new StateChangeSortMode() };
//        var transparentRenderStage = new RenderStage("Transparent", "Main") { SortMode = new BackToFrontSortMode() };
//        var gBufferRenderStage = new RenderStage("GBuffer", "GBuffer") { SortMode = new FrontToBackSortMode() };

//        var forwardRenderer = new ForwardRenderer
//        {
//            Clear = { Color = clearColor },
//            OpaqueRenderStage = opaqueRenderStage,
//            TransparentRenderStage = transparentRenderStage,
//            GBufferRenderStage = gBufferRenderStage,
//            LightProbes = false,
//        };
//        var renderTextureSceneRenderer = new RenderTextureSceneRenderer
//        {
//            Child = forwardRenderer
//        };

//        var cameraSlot = new SceneCameraSlot
//        {
//            Name = "RenderPaintingPickCameraSlot",
//        };

//        return new GraphicsCompositor
//        {
//            Name = "RenderPaintingPickGraphicsCompositor",
//            Cameras = { cameraSlot, },
//            RenderStages =
//            {
//                opaqueRenderStage,
//                transparentRenderStage,
//                gBufferRenderStage,
//            },
//            RenderFeatures =
//            {
//                new MeshRenderFeature
//                {
//                    RenderFeatures =
//                    {
//                        new TransformRenderFeature(),
//                        new SkinningRenderFeature(),
//                        new MaterialRenderFeature(),
//                        new InstancingRenderFeature(),
//                        new PaintingPickRenderFeature(),
//                    },
//                    RenderStageSelectors =
//                    {
//                        new MeshTransparentRenderStageSelector
//                        {
//                            EffectName = RenderEffectName,
//                            OpaqueRenderStage = opaqueRenderStage,
//                            TransparentRenderStage = transparentRenderStage,
//                            RenderGroup = groupMask,
//                        },
//                        //new MeshTransparentRenderStageSelector
//                        //{
//                        //    EffectName = RenderEffectName,
//                        //    OpaqueRenderStage = gBufferRenderStage,
//                        //    //TransparentRenderStage = transparentRenderStage,
//                        //    RenderGroup = groupMask,
//                        //},
//                    },
//                    PipelineProcessors =
//                    {
//                        new MeshPipelineProcessor { TransparentRenderStage = transparentRenderStage },
//                    },
//                },
//                //new SpriteRenderFeature
//                //{
//                //    RenderStageSelectors =
//                //    {
//                //        new SpriteTransparentRenderStageSelector
//                //        {
//                //            EffectName = "Test",
//                //            OpaqueRenderStage = opaqueRenderStage,
//                //            TransparentRenderStage = transparentRenderStage,
//                //            RenderGroup = groupMask,
//                //        },
//                //    },
//                //},
//                //new BackgroundRenderFeature
//                //{
//                //    RenderStageSelectors =
//                //    {
//                //        new SimpleGroupToRenderStageSelector
//                //        {
//                //            RenderStage = opaqueRenderStage,
//                //            EffectName = "Test",
//                //            RenderGroup = groupMask,
//                //        },
//                //    },
//                //},
//            },
//            Game = new SceneCameraRenderer()
//            {
//                Child = renderTextureSceneRenderer,
//                Camera = cameraSlot,
//            },
//            Editor = renderTextureSceneRenderer,
//            SingleView = renderTextureSceneRenderer,
//        };
//    }

//    private static Entity CreateCameraEntity(in CameraSettings cameraSettings, in TransformTRS cameraTransformTRS, GraphicsCompositor graphicsCompositor)
//    {
//        var cameraSlot = graphicsCompositor.Cameras[0];
//        var cameraComponent = new CameraComponent
//        {
//            Slot = cameraSlot.ToSlotId(),
//        };
//        cameraSettings.ToCameraComponent(cameraComponent);

//        var cameraEntity = new Entity("Render Painting Pick Camera") { cameraComponent };
//        cameraTransformTRS.SetToEntity(cameraEntity);
//        return cameraEntity;
//    }
//}

//public class zzzRenderTextureSceneRenderer : SceneRendererBase
//{
//    public Texture? RenderTexture { get; set; }

//    public ISceneRenderer Child { get; set; }

//    protected override void CollectCore(RenderContext context)
//    {
//        base.CollectCore(context);

//        if (RenderTexture == null)
//            return;

//        using (context.SaveRenderOutputAndRestore())
//        using (context.SaveViewportAndRestore())
//        {
//            context.RenderOutput.RenderTargetFormat0 = RenderTexture.ViewFormat;
//            context.RenderOutput.RenderTargetCount = 1;
//            context.ViewportState.Viewport0 = new Viewport(0, 0, RenderTexture.ViewWidth, RenderTexture.ViewHeight);

//            Child?.Collect(context);
//        }
//    }

//    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
//    {
//        if (RenderTexture == null)
//            return;

//        using (drawContext.PushRenderTargetsAndRestore())
//        {
//            var depthBufferTextureFlags = TextureFlags.DepthStencil;
//            if (context.GraphicsDevice.Features.HasDepthAsSRV)
//                depthBufferTextureFlags |= TextureFlags.ShaderResource;

//            var depthBuffer = PushScopedResource(context.Allocator.GetTemporaryTexture2D(RenderTexture.ViewWidth, RenderTexture.ViewHeight, drawContext.CommandList.DepthStencilBuffer.ViewFormat, depthBufferTextureFlags));
//            drawContext.CommandList.SetRenderTargetAndViewport(depthBuffer, RenderTexture);

//            Child?.Draw(drawContext);
//        }
//    }
//}
//public class zzzEditorGameCameraPreviewService : EditorGameServiceBase, IEditorGameCameraPreviewService, IEditorGameCameraPreviewViewModelService
//{
//    private readonly IEditorGameController controller;
//    private SpriteBatch incrustBatch;
//    private bool isActive;
//    private Entity selectedEntity;
//    private Graphics.SpriteFont defaultFont;

//    private EntityHierarchyEditorGame game;

//    private EffectInstance spriteEffect;
//    private MutablePipelineState borderPipelineState;
//    private Graphics.Buffer borderVertexBuffer;

//    private GenerateIncrustRenderer generateIncrustRenderer;
//    private RenderIncrustRenderer renderIncrustRenderer;

//    public EditorGameCameraPreviewService(IEditorGameController controller)
//    {
//        this.controller = controller;
//    }

//    public override bool IsActive { get { return isActive; } set { isActive = value; } }

//    public override IEnumerable<Type> Dependencies { get { yield return typeof(IEditorGameEntitySelectionService); yield return typeof(IEditorGameRenderModeService); } }

//    bool IEditorGameCameraPreviewViewModelService.IsActive { get { return isActive; } set { isActive = value; controller.InvokeAsync(() => IsActive = value); } }

//    private IEditorGameRenderModeService Rendering => game.EditorServices.Get<IEditorGameRenderModeService>();

//    protected override Task<bool> Initialize(EditorServiceGame editorGame)
//    {
//        game = (EntityHierarchyEditorGame)editorGame;

//        // create the default font
//        var fontItem = OfflineRasterizedSpriteFontFactory.Create();
//        fontItem.FontType.Size = 8;
//        defaultFont = OfflineRasterizedFontCompiler.Compile(game.Services.GetService<IFontFactory>(), fontItem, game.GraphicsDevice.ColorSpace == ColorSpace.Linear);

//        incrustBatch = new SpriteBatch(game.GraphicsDevice);

//        // SpriteEffect will be used to draw camera incrust border
//        spriteEffect = new EffectInstance(new Graphics.Effect(game.GraphicsDevice, SpriteEffect.Bytecode));
//        spriteEffect.Parameters.Set(TexturingKeys.Texture0, game.GraphicsDevice.GetSharedWhiteTexture());
//        spriteEffect.UpdateEffect(game.GraphicsDevice);

//        borderPipelineState = new MutablePipelineState(game.GraphicsDevice);
//        borderPipelineState.State.RootSignature = spriteEffect.RootSignature;
//        borderPipelineState.State.EffectBytecode = spriteEffect.Effect.Bytecode;
//        borderPipelineState.State.InputElements = VertexPositionTexture.Layout.CreateInputElements();
//        borderPipelineState.State.PrimitiveType = PrimitiveType.LineStrip;
//        borderPipelineState.State.RasterizerState = RasterizerStates.CullNone;

//        var borderVertices = new[]
//        {
//                new VertexPositionTexture(new Vector3(0.0f, 0.0f, 0.0f), Vector2.Zero),
//                new VertexPositionTexture(new Vector3(0.0f, 1.0f, 0.0f), Vector2.Zero),
//                new VertexPositionTexture(new Vector3(1.0f, 1.0f, 0.0f), Vector2.Zero),
//                new VertexPositionTexture(new Vector3(1.0f, 0.0f, 0.0f), Vector2.Zero),
//                new VertexPositionTexture(new Vector3(0.0f, 0.0f, 0.0f), Vector2.Zero),
//                new VertexPositionTexture(new Vector3(0.0f, 1.0f, 0.0f), Vector2.Zero), // extra vertex so that left-top corner is not missing (likely due to rasterization rule)
//            };
//        borderVertexBuffer = Graphics.Buffer.Vertex.New(game.GraphicsDevice, borderVertices);

//        if (game.EditorSceneSystem.GraphicsCompositor.Game is EditorTopLevelCompositor editorTopLevel)
//        {
//            // Display it as incrust
//            editorTopLevel.PostGizmoCompositors.Add(renderIncrustRenderer = new RenderIncrustRenderer(this));
//        }

//        Services.Get<IEditorGameEntitySelectionService>().SelectionUpdated += UpdateModifiedEntitiesList;

//        return Task.FromResult(true);
//    }

//    public override void UpdateGraphicsCompositor([NotNull] EditorServiceGame game)
//    {
//        base.UpdateGraphicsCompositor(game);

//        renderIncrustRenderer.IncrustRenderer = null;

//        var gameTopLevel = game.SceneSystem.GraphicsCompositor.Game as EditorTopLevelCompositor;
//        if (gameTopLevel != null)
//        {
//            generateIncrustRenderer = new GenerateIncrustRenderer(this)
//            {
//                Content = gameTopLevel.Child,
//                GameSettingsAccessor = game.PackageSettings,
//            };

//            // Render camera view
//            gameTopLevel.PostGizmoCompositors.Add(generateIncrustRenderer);

//            renderIncrustRenderer.IncrustRenderer = generateIncrustRenderer;
//        }
//    }

//    public override Task DisposeAsync()
//    {
//        EnsureNotDestroyed(nameof(EditorGameCameraPreviewService));

//        // Unregister events
//        var selectionService = Services.Get<IEditorGameEntitySelectionService>();
//        if (selectionService != null)
//            selectionService.SelectionUpdated -= UpdateModifiedEntitiesList;

//        // Remove renderers
//        var gameTopLevel = game.SceneSystem.GraphicsCompositor.Game as EditorTopLevelCompositor;
//        var editorTopLevel = game.EditorSceneSystem.GraphicsCompositor.Game as EditorTopLevelCompositor;
//        if (gameTopLevel != null && editorTopLevel != null)
//        {
//            gameTopLevel.PostGizmoCompositors.Remove(generateIncrustRenderer);
//            editorTopLevel.PostGizmoCompositors.Remove(renderIncrustRenderer);
//        }

//        defaultFont?.Dispose();
//        defaultFont = null;

//        spriteEffect?.Dispose();
//        spriteEffect = null;

//        borderPipelineState = null;
//        borderVertexBuffer?.Dispose();
//        borderVertexBuffer = null;

//        return base.DisposeAsync();
//    }

//    private void UpdateModifiedEntitiesList(object sender, EntitySelectionEventArgs e)
//    {
//        selectedEntity = e.NewSelection.Count == 1 ? e.NewSelection.Single() : null;
//    }

//    class RenderIncrustRenderer : SceneRendererBase
//    {
//        private readonly EditorGameCameraPreviewService previewService;
//        public GenerateIncrustRenderer IncrustRenderer { get; set; }

//        public RenderIncrustRenderer(EditorGameCameraPreviewService previewService)
//        {
//            this.previewService = previewService;
//        }

//        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
//        {
//            if (IncrustRenderer == null || !IncrustRenderer.IsIncrustEnabled)
//                return;

//            var currentViewport = drawContext.CommandList.Viewport;

//            // Copy incrust back to render target
//            var position = new Vector2(currentViewport.Width - IncrustRenderer.Viewport.Width - 16, currentViewport.Height - IncrustRenderer.Viewport.Height - 16);

//            // Camera incrust border
//            previewService.borderPipelineState.State.Output.CaptureState(drawContext.CommandList);
//            previewService.borderPipelineState.Update();
//            drawContext.CommandList.SetPipelineState(previewService.borderPipelineState.CurrentState);
//            previewService.spriteEffect.Parameters.Set(SpriteBaseKeys.MatrixTransform, Matrix.Scaling((float)(IncrustRenderer.Viewport.Width + 1) * 2.0f / (float)currentViewport.Width, -(float)(IncrustRenderer.Viewport.Height + 1) * 2.0f / (float)currentViewport.Height, 1.0f) * Matrix.Translation((float)position.X * 2.0f / (float)currentViewport.Width - 1.0f, -((float)position.Y * 2.0f / (float)currentViewport.Height - 1.0f), 0.0f));
//            previewService.spriteEffect.Apply(drawContext.GraphicsContext);

//            drawContext.CommandList.SetVertexBuffer(0, previewService.borderVertexBuffer, 0, VertexPositionTexture.Size);
//            drawContext.CommandList.Draw(previewService.borderVertexBuffer.SizeInBytes / VertexPositionTexture.Size);

//            // Camera incrust render
//            previewService.incrustBatch.Begin(drawContext.GraphicsContext, blendState: BlendStates.Default, depthStencilState: DepthStencilStates.None);
//            previewService.incrustBatch.Draw(IncrustRenderer.GeneratedIncrust, position);
//            previewService.incrustBatch.End();

//            // Camera name
//            if (IncrustRenderer.Camera?.Entity?.Name != null)
//            {
//                previewService.incrustBatch.Begin(drawContext.GraphicsContext, blendState: BlendStates.AlphaBlend, depthStencilState: DepthStencilStates.None);
//                previewService.incrustBatch.DrawString(previewService.defaultFont, IncrustRenderer.Camera?.Entity.Name, new Vector2(position.X, position.Y - 16), Color.White);
//                previewService.incrustBatch.End();
//            }

//            drawContext.GraphicsContext.Allocator.ReleaseReference(IncrustRenderer.GeneratedIncrust);
//        }
//    }

//    class GenerateIncrustRenderer : SceneRendererBase
//    {
//        private readonly EditorGameCameraPreviewService previewService;
//        internal bool IsIncrustEnabled;
//        internal CameraComponent Camera;

//        public GenerateIncrustRenderer(EditorGameCameraPreviewService previewService)
//        {
//            this.previewService = previewService;
//        }

//        /// <summary>
//        /// The inner compositor to draw inside the viewport.
//        /// </summary>
//        public new ISceneRenderer Content { get; set; }

//        public IGameSettingsAccessor GameSettingsAccessor { get; set; }

//        /// <summary>
//        /// The render view created and used by this compositor.
//        /// </summary>
//        public RenderView RenderView { get; } = new RenderView();

//        public Viewport Viewport;

//        public Texture GeneratedIncrust { get; private set; }

//        protected override void CollectCore(RenderContext context)
//        {
//            base.CollectCore(context);

//            Camera = previewService.selectedEntity?.Components.Get<CameraComponent>();

//            // Enable camera incrust only if we have an active camera and IsActive is true
//            // Also disabled when previewing game graphics compositor
//            IsIncrustEnabled = previewService.isActive && !previewService.Rendering.RenderMode.PreviewGameGraphicsCompositor && Camera != null;

//            if (IsIncrustEnabled)
//            {
//                var width = context.ViewportState.Viewport0.Width * 0.3f;
//                var height = context.ViewportState.Viewport0.Height * 0.2f;
//                var aspectRatio = width / height;

//                if (Camera.UseCustomAspectRatio)
//                {
//                    // Make sure to respect aspect ratio
//                    aspectRatio = Camera.AspectRatio;
//                }
//                else if (GameSettingsAccessor != null)
//                {
//                    var renderingSettings = GameSettingsAccessor.GetConfiguration<RenderingSettings>();
//                    if (renderingSettings != null)
//                    {
//                        aspectRatio = (float)renderingSettings.DefaultBackBufferWidth / (float)renderingSettings.DefaultBackBufferHeight;
//                    }
//                }

//                if (width > height * aspectRatio)
//                {
//                    width = height * aspectRatio;
//                }
//                else
//                {
//                    height = width / aspectRatio;
//                }

//                if (width < 32.0f || height < 32.0f)
//                {
//                    // Do not display incrust if too small
//                    IsIncrustEnabled = false;
//                }

//                context.RenderSystem.Views.Add(RenderView);
//                context.RenderView = RenderView;

//                // Setup viewport
//                Viewport = new Viewport(0, 0, (int)width, (int)height);

//                using (context.SaveViewportAndRestore())
//                {
//                    context.ViewportState = new ViewportState { Viewport0 = Viewport };

//                    SceneCameraRenderer.UpdateCameraToRenderView(context, RenderView, Camera);

//                    using (context.PushRenderViewAndRestore(RenderView))
//                    using (context.PushTagAndRestore(CameraComponentRendererExtensions.Current, Camera))
//                    {
//                        Content.Collect(context);
//                    }
//                }
//            }
//        }

//        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
//        {
//            if (!IsIncrustEnabled)
//                return;

//            using (context.PushRenderViewAndRestore(RenderView))
//            using (drawContext.PushTagAndRestore(CameraComponentRendererExtensions.Current, Camera))
//            {
//                var oldViewport = drawContext.CommandList.Viewport;

//                // Allocate a RT for the incrust
//                GeneratedIncrust = drawContext.GraphicsContext.Allocator.GetTemporaryTexture2D(TextureDescription.New2D((int)Viewport.Width, (int)Viewport.Height, 1, drawContext.CommandList.RenderTarget.Format, TextureFlags.ShaderResource | TextureFlags.RenderTarget));

//                var depthBufferTextureFlags = TextureFlags.DepthStencil;
//                if (context.GraphicsDevice.Features.HasDepthAsSRV)
//                    depthBufferTextureFlags |= TextureFlags.ShaderResource;

//                var depthBuffer = drawContext.CommandList.DepthStencilBuffer != null ? drawContext.GraphicsContext.Allocator.GetTemporaryTexture2D(TextureDescription.New2D((int)Viewport.Width, (int)Viewport.Height, 1, drawContext.CommandList.DepthStencilBuffer.Format, depthBufferTextureFlags)) : null;

//                // Push and set render target
//                using (drawContext.PushRenderTargetsAndRestore())
//                {
//                    drawContext.CommandList.SetRenderTarget(depthBuffer, GeneratedIncrust);

//                    drawContext.CommandList.SetViewport(Viewport);
//                    Content.Draw(drawContext);

//                    drawContext.CommandList.SetViewport(oldViewport);
//                }

//                // Note: GeneratedIncrust is released by RenderIncrustCompositorPart
//                if (depthBuffer != null)
//                    drawContext.GraphicsContext.Allocator.ReleaseReference(depthBuffer);
//            }
//        }
//    }
//}