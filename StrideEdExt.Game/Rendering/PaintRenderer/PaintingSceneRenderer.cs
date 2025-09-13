using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using StrideEdExt.Painting;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StrideEdExt.Rendering.PaintRenderer;

class PaintingSceneRenderer : SceneRendererBase, IPaintRendererService
{
    private Texture? _pickingDepthStencil;
    private Texture? _pickingHitRenderTarget;
    private Texture? _pickWorldPositionRenderTarget;
    private Texture? _pickWorldNormalRenderTarget;
    private Texture? _pickingHitTexture;
    private Texture? _pickWorldPositionTexture;
    private Texture? _pickWorldNormalTexture;
    private PaintSessionId _paintSessionId;

    private DynamicEffectInstance _strokeMapPaintingEffect = default!;
    private MutablePipelineState _strokeMapPaintingPipelineState = default!;

    private readonly List<BrushRenderArgs> _pendingBrushRenders = [];

    public RenderStage? PaintingRenderStage { get; set; }

    protected override void InitializeCore()
    {
        base.InitializeCore();

        _strokeMapPaintingEffect = new DynamicEffectInstance("StrokeMapPaintingOutputEffect");
        _strokeMapPaintingEffect.Initialize(Context.Services);

        _strokeMapPaintingPipelineState = new MutablePipelineState(Context.GraphicsDevice);
        var stateDesc = _strokeMapPaintingPipelineState.State;
        stateDesc.SetDefaults();
        stateDesc.InputElements = VertexPositionNormalTexture.Layout.CreateInputElements();
        stateDesc.BlendState = BlendStates.AlphaBlend;
        stateDesc.DepthStencilState = DepthStencilStates.Default;
        stateDesc.RasterizerState = RasterizerStates.CullNone;
    }

    protected override void Unload()
    {
        base.Unload();

        DisposableExtensions.DisposeAndNull(ref _pickingDepthStencil);
        DisposableExtensions.DisposeAndNull(ref _pickingHitRenderTarget);
        DisposableExtensions.DisposeAndNull(ref _pickWorldPositionRenderTarget);
        DisposableExtensions.DisposeAndNull(ref _pickWorldNormalRenderTarget);
        DisposableExtensions.DisposeAndNull(ref _pickingHitTexture);
        DisposableExtensions.DisposeAndNull(ref _pickWorldPositionTexture);
        DisposableExtensions.DisposeAndNull(ref _pickWorldNormalTexture);
    }

    protected override void CollectCore(RenderContext context)
    {
        // Fill RenderStage formats
        // This declares the PaintingPick texture to be (uint) format.
        // Changing this means changing the output of PaintingPickOutputShader.
        if (PaintingRenderStage is null)
        {
            return;
        }

        PaintingRenderStage.Output = new RenderOutputDescription(renderTargetFormat: PixelFormat.R8_UInt, depthStencilFormat: PixelFormat.D32_Float);
        PaintingRenderStage.Output.RenderTargetFormat1 = PixelFormat.R32G32B32A32_Float;    // World Position
        PaintingRenderStage.Output.RenderTargetFormat2 = PixelFormat.R32G32B32A32_Float;    // World Normal
        PaintingRenderStage.Output.RenderTargetCount = 3;

        // Note: if context.RenderView is null, then most likely the GraphicsCompositor is not
        // set up correctly. Ensure this renderer is a child of a CameraRenderer.
        context.RenderView.RenderStages.Add(PaintingRenderStage);
    }

    private Texture[] _renderTargets = new Texture[3];
    private IPainterService? _painterService;
    private readonly List<Vector2> _renderBrushDataList = [];
    private TextureBrushData[]? _textureBrushDataArray;
    private Buffer<TextureBrushData>? _textureBrushDataBuffer;
    private readonly Dictionary<PaintTargetEntityMesh, Texture> _entityMeshToStrokeMapTextureMap = [];
    protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
    {
        if (PaintingRenderStage is null)
        {
            return;
        }

        _painterService ??= Services.GetService<IPainterService>();
        if (_painterService is null)
        {
            return;
        }
        if (!_painterService.TryGetActiveSessionId(out _paintSessionId))
        {
            return;
        }

        var graphicsDevice = drawContext.GraphicsDevice;
        var commandList = drawContext.CommandList;

        var viewSize = context.RenderView.ViewSize;
        var viewWidth = (int)viewSize.X;
        var viewHeight = (int)viewSize.Y;

        var pendingBrushRendersSpan = CollectionsMarshal.AsSpan(_pendingBrushRenders);
        for (int i = 0; i < pendingBrushRendersSpan.Length; i++)
        {
            ref var pendingRender = ref pendingBrushRendersSpan[i];
            var brushPointsSpan = CollectionsMarshal.AsSpan(pendingRender.BrushPoints);
            if (_textureBrushDataArray is null || _textureBrushDataArray.Length != brushPointsSpan.Length)
            {
                _textureBrushDataArray = new TextureBrushData[brushPointsSpan.Length];
            }
            for (int j = 0; j < brushPointsSpan.Length; j++)
            {
                ref var brushPoint = ref brushPointsSpan[j];
                var scaling = Vector3.One;
                var rotation = Quaternion.Identity;     // TODO?
                var translation = brushPoint.WorldPosition;
                Matrix.Transformation(in scaling, in rotation, in translation, out var transformMatrix);
                Matrix.Invert(in transformMatrix, out var transformInverseMatrix);
                _textureBrushDataArray[j] = new TextureBrushData
                {
                    WorldPosition = brushPoint.WorldPosition,
                    WorldNormal = brushPoint.WorldNormal,
                    BrushWorldInverse = transformInverseMatrix,
                    Strength = brushPoint.Strength,
                };
            }

            if (_textureBrushDataBuffer is null || _textureBrushDataBuffer.ElementCount < brushPointsSpan.Length)
            {
                // Create buffer or recreate to fit new data size
                _textureBrushDataBuffer?.Dispose();
                _textureBrushDataBuffer = graphicsDevice.CreateShaderBuffer<TextureBrushData>(brushPointsSpan.Length);
            }
            _textureBrushDataBuffer.SetData(commandList, _textureBrushDataArray);

            var effectParams = _strokeMapPaintingEffect.Parameters;
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.TextureBrushCount, pendingBrushRendersSpan.Length);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.TextureBrushArray, _textureBrushDataBuffer);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.BrushModeType, (uint)pendingRender.BrushModeType);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.BrushRadius, pendingRender.BrushRadius);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.BrushOpacity, pendingRender.BrushOpacity);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.BrushFalloffType, (uint)pendingRender.BrushFalloffType);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.FalloffStartPercentage, pendingRender.FalloffStartPercentage);
            effectParams.Set(StrokeMapPaintingOutputShaderKeys.BrushTexture, pendingRender.BrushTexture);

            // Render brush stroke map
            foreach (var renderObject in context.RenderView.RenderObjects)
            {
                if (renderObject is not RenderMesh renderMesh)
                {
                    continue;
                }
                if (renderMesh.Source is not ModelComponent modelComponent)
                {
                    throw new InvalidOperationException($"RenderMesh source was not ModelComponent.");
                    //continue;
                }
                var targetEntityMesh = new PaintTargetEntityMesh
                {
                    EntityId = modelComponent.Entity.Id,
                    Mesh = renderMesh.Mesh
                };
                if (!pendingRender.TargetEntityMeshToStrokeMapTextureMap.TryGetValue(targetEntityMesh, out var renderTargetData))
                {
                    continue;
                }

                var strokeMapRenderTarget = renderTargetData.Texture;
                var strokeMapDepthStencil = PushScopedResource(context.Allocator.GetTemporaryTexture2D(strokeMapRenderTarget.Width, strokeMapRenderTarget.Height, PixelFormat.D32_Float, TextureFlags.DepthStencil));
                var previousStrokeMapRenderTarget = PushScopedResource(context.Allocator.GetTemporaryTexture2D(strokeMapRenderTarget.Width, strokeMapRenderTarget.Height, strokeMapRenderTarget.Format));
                if (renderTargetData.IsNewTexture)
                {
                    Debug.WriteLine($"New stroke map texture. Clearing previousStrokeMapRenderTarget.");
                    commandList.Clear(previousStrokeMapRenderTarget, Color.Transparent);
                    renderTargetData.IsNewTexture = false;
                }
                else
                {
                    commandList.Copy(source: strokeMapRenderTarget, destination: previousStrokeMapRenderTarget);
                }
                effectParams.Set(StrokeMapPaintingOutputShaderKeys.PreviousStrokeMapTexture, previousStrokeMapRenderTarget);

                var worldMatrix = renderMesh.World;
                Matrix.Invert(in worldMatrix, out var worldInverseMatrix);
                Matrix.Transpose(in worldInverseMatrix, out var worldInverseTransposeMatrix);
                effectParams.Set(TransformationKeys.World, worldMatrix);
                effectParams.Set(TransformationKeys.WorldInverse, worldInverseMatrix);
                effectParams.Set(TransformationKeys.WorldInverseTranspose, worldInverseTransposeMatrix);
                effectParams.Set(TransformationKeys.WorldView, Matrix.Identity);
                effectParams.Set(TransformationKeys.WorldViewInverse, Matrix.Identity);
                effectParams.Set(TransformationKeys.WorldViewProjection, Matrix.Identity);
                effectParams.Set(TransformationKeys.WorldScale, Vector3.One);
                //effectParams.Set(TransformationKeys.EyeMS, ); // Unused

                _strokeMapPaintingEffect.UpdateEffect(graphicsDevice);

                var meshDraw = renderMesh.ActiveMeshDraw;

                using (drawContext.PushRenderTargetsAndRestore())
                {
                    //commandList.Reset();
                    //commandList.ClearState();

                    // Transfer state to all command lists
                    //commandList.SetViewport(viewport);
                    //commandList.SetScissorRectangle(scissor);
                    commandList.ResourceBarrierTransition(strokeMapDepthStencil, GraphicsResourceState.DepthWrite);
                    commandList.Clear(strokeMapDepthStencil, DepthStencilClearOptions.DepthBuffer);

                    commandList.ResourceBarrierTransition(strokeMapRenderTarget, GraphicsResourceState.RenderTarget);
                    commandList.Clear(strokeMapRenderTarget, Color.Transparent);

                    commandList.SetRenderTargetAndViewport(strokeMapDepthStencil, strokeMapRenderTarget);

                    var stateDesc = _strokeMapPaintingPipelineState.State;
                    stateDesc.RootSignature = _strokeMapPaintingEffect.RootSignature;
                    stateDesc.EffectBytecode = _strokeMapPaintingEffect.Effect.Bytecode;
                    stateDesc.InputElements = PrepareInputElements(stateDesc, meshDraw);
                    stateDesc.PrimitiveType = meshDraw.PrimitiveType;

                    stateDesc.Output.CaptureState(commandList);
                    _strokeMapPaintingPipelineState.Update();
                    commandList.SetPipelineState(_strokeMapPaintingPipelineState.CurrentState);

                    _strokeMapPaintingEffect.Apply(drawContext.GraphicsContext);

                    for (int slotIndex = 0; slotIndex < meshDraw.VertexBuffers.Length; slotIndex++)
                    {
                        var vertexBuffer = meshDraw.VertexBuffers[slotIndex];
                        commandList.SetVertexBuffer(slotIndex, vertexBuffer.Buffer, vertexBuffer.Offset, vertexBuffer.Stride);
                    }
                    if (meshDraw.IndexBuffer is not null)
                    {
                        commandList.SetIndexBuffer(meshDraw.IndexBuffer.Buffer, meshDraw.IndexBuffer.Offset, meshDraw.IndexBuffer.Is32Bit);
                    }

                    if (meshDraw.IndexBuffer is not null)
                    {
                        commandList.DrawIndexed(meshDraw.DrawCount, meshDraw.StartLocation);
                    }
                    else
                    {
                        commandList.Draw(meshDraw.DrawCount, meshDraw.StartLocation);
                    }

                    _entityMeshToStrokeMapTextureMap[targetEntityMesh] = strokeMapRenderTarget;
                }
            }
            foreach (var (_, texture) in _entityMeshToStrokeMapTextureMap)
            {
                commandList.ResourceBarrierTransition(texture, GraphicsResourceState.PixelShaderResource);
            }
            if (pendingRender.RenderCompletedCallback is not null)
            {
                pendingRender.RenderCompletedCallback.Invoke(_entityMeshToStrokeMapTextureMap.ToDictionary());
            }

            _entityMeshToStrokeMapTextureMap.Clear();   // We don't want to retain a reference to the Textures.
        }
        _pendingBrushRenders.Clear();

        RenderingPickingTexture(context, drawContext);
        //commandList.Close();    //?
    }

    private void RenderingPickingTexture(RenderContext context, RenderDrawContext drawContext)
    {
        Debug.Assert(PaintingRenderStage is not null);

        var graphicsDevice = drawContext.GraphicsDevice;
        var commandList = drawContext.CommandList;

        var viewSize = context.RenderView.ViewSize;
        var viewWidth = (int)viewSize.X;
        var viewHeight = (int)viewSize.Y;

        if (_pickingHitRenderTarget is null
            || _pickingHitRenderTarget.Width != viewWidth || _pickingHitRenderTarget.Height != viewHeight)
        {
            _pickingHitRenderTarget?.Dispose();
            _pickingHitRenderTarget = Texture.New2D(graphicsDevice,
                viewWidth, viewHeight, format: PaintingRenderStage.Output.RenderTargetFormat0,
                textureFlags: TextureFlags.ShaderResource | TextureFlags.RenderTarget,
                arraySize: 1, usage: GraphicsResourceUsage.Staging);
        }
        if (_pickWorldPositionRenderTarget is null
            || _pickWorldPositionRenderTarget.Width != viewWidth || _pickWorldPositionRenderTarget.Height != viewHeight)
        {
            _pickWorldPositionRenderTarget?.Dispose();
            _pickWorldPositionRenderTarget = Texture.New2D(graphicsDevice,
                viewWidth, viewHeight, format: PaintingRenderStage.Output.RenderTargetFormat1,
                textureFlags: TextureFlags.ShaderResource | TextureFlags.RenderTarget,
                arraySize: 1, usage: GraphicsResourceUsage.Staging);
        }
        if (_pickWorldNormalRenderTarget is null
            || _pickWorldNormalRenderTarget.Width != viewWidth || _pickWorldNormalRenderTarget.Height != viewHeight)
        {
            _pickWorldNormalRenderTarget?.Dispose();
            _pickWorldNormalRenderTarget = Texture.New2D(graphicsDevice,
                viewWidth, viewHeight, format: PaintingRenderStage.Output.RenderTargetFormat2,
                textureFlags: TextureFlags.ShaderResource | TextureFlags.RenderTarget,
                arraySize: 1, usage: GraphicsResourceUsage.Staging);
        }

        if (_pickingDepthStencil is null
            || _pickingDepthStencil.Width != viewWidth || _pickingDepthStencil.Height != viewHeight)
        {
            _pickingDepthStencil?.Dispose();
            _pickingDepthStencil = Texture.New2D(graphicsDevice,
                viewWidth, viewHeight, format: PaintingRenderStage.Output.DepthStencilFormat,
                textureFlags: TextureFlags.DepthStencil,
                arraySize: 1, usage: GraphicsResourceUsage.Default);
        }

        _renderTargets[0] = _pickingHitRenderTarget;
        _renderTargets[1] = _pickWorldPositionRenderTarget;
        _renderTargets[2] = _pickWorldNormalRenderTarget;

        // Render the picking stage using the current view
        using (drawContext.PushRenderTargetsAndRestore())
        {
            commandList.ResourceBarrierTransition(_pickingDepthStencil, GraphicsResourceState.DepthWrite);
            commandList.Clear(_pickingDepthStencil, DepthStencilClearOptions.DepthBuffer);

            foreach (var tex in _renderTargets)
            {
                commandList.ResourceBarrierTransition(tex, GraphicsResourceState.RenderTarget);
                commandList.Clear(tex, Color.Transparent);
            }

            commandList.SetRenderTargetsAndViewport(_pickingDepthStencil, _renderTargets);
            context.RenderSystem.Draw(drawContext, context.RenderView, PaintingRenderStage);
        }

        // Prepare as a shader resource view to be accessible in other render stages
        foreach (var tex in _renderTargets)
        {
            commandList.ResourceBarrierTransition(tex, GraphicsResourceState.PixelShaderResource);
        }
        Array.Clear(_renderTargets);
    }

    private InputElementDescription[] PrepareInputElements(PipelineStateDescription pipelineState, MeshDraw drawData)
    {
        // Code from MeshRenderFeature.PrepareInputElements

        // Get the input elements already contained in the mesh's vertex buffers
        var availableInputElements = drawData.VertexBuffers.CreateInputElements();
        var inputElements = new List<InputElementDescription>(availableInputElements);

        // In addition, add input elements for all attributes that are not contained in a bound buffer, but required by the shader
        foreach (var inputAttribute in pipelineState.EffectBytecode.Reflection.InputAttributes)
        {
            var inputElementIndex = FindElementBySemantic(availableInputElements, inputAttribute.SemanticName, inputAttribute.SemanticIndex);

            // Provided by any vertex buffer?
            if (inputElementIndex >= 0)
                continue;

            inputElements.Add(new InputElementDescription
            {
                AlignedByteOffset = 0,
                Format = PixelFormat.R32G32B32A32_Float,
                InputSlot = drawData.VertexBuffers.Length,
                InputSlotClass = InputClassification.Vertex,
                InstanceDataStepRate = 0,
                SemanticIndex = inputAttribute.SemanticIndex,
                SemanticName = inputAttribute.SemanticName,
            });
        }

        return inputElements.ToArray();

        static int FindElementBySemantic(InputElementDescription[] inputElements, string semanticName, int semanticIndex)
        {
            int foundDescIndex = -1;
            for (int index = 0; index < inputElements.Length; index++)
            {
                if (semanticName == inputElements[index].SemanticName && semanticIndex == inputElements[index].SemanticIndex)
                    foundDescIndex = index;
            }

            return foundDescIndex;
        }
    }

    public void EnqueueBrushRender(BrushRenderArgs brushRenderArgs)
    {
        _pendingBrushRenders.Add(brushRenderArgs);
    }

    private IGame? _game;
    public PaintingCursorHitResultData GetCursorHitResult(Vector2 screenPositionNormalized)
    {
        _game ??= Services.GetSafeServiceAs<IGame>();
        var commandList = _game.GraphicsContext.CommandList;
        var graphicsDevice = _game.GraphicsDevice;
        var pickHitResultData = GetCursorHitResultInternal(screenPositionNormalized, commandList, graphicsDevice);
        return pickHitResultData;
    }

    private PaintingCursorHitResultData GetCursorHitResultInternal(Vector2 screenPositionNormalized, CommandList commandList, GraphicsDevice graphicsDevice)
    {
        if (_pickingHitRenderTarget is null
             || _pickWorldPositionRenderTarget is null
             || _pickWorldNormalRenderTarget is null
             || PaintingRenderStage is null)
        {
            return default;
        }

        _pickingHitTexture ??= Texture.New2D(graphicsDevice,
            width: 1, height: 1, format: PaintingRenderStage.Output.RenderTargetFormat0,
            textureFlags: TextureFlags.None, arraySize: 1, usage: GraphicsResourceUsage.Staging);
        _pickWorldPositionTexture ??= Texture.New2D(graphicsDevice,
            width: 1, height: 1, format: PaintingRenderStage.Output.RenderTargetFormat1,
            textureFlags: TextureFlags.None, arraySize: 1, usage: GraphicsResourceUsage.Staging);
        _pickWorldNormalTexture ??= Texture.New2D(graphicsDevice,
            width: 1, height: 1, format: PaintingRenderStage.Output.RenderTargetFormat2,
            textureFlags: TextureFlags.None, arraySize: 1, usage: GraphicsResourceUsage.Staging);

        Span<byte> pickingHitData = stackalloc byte[1];
        Span<Vector3> pickWorldPositionData = stackalloc Vector3[1];
        Span<Vector3> pickWorldNormalData = stackalloc Vector3[1];

        var viewSize = new Vector2(_pickingHitRenderTarget.Width, _pickingHitRenderTarget.Height);
        var textureUv = screenPositionNormalized * viewSize;
        int textureX = MathUtil.Clamp((int)textureUv.X, min: 0, max: (int)viewSize.X - 1);
        int textureY = MathUtil.Clamp((int)textureUv.Y, min: 0, max: (int)viewSize.Y - 1);
        // Copy results to 1x1 target
        var textureRegion = new ResourceRegion(left: textureX, top: textureY, front: 0, right: textureX + 1, bottom: textureY + 1, back: 1);
        const int SubResourceIndex = 0;
        commandList.CopyRegion(_pickingHitRenderTarget, SubResourceIndex, textureRegion, _pickingHitTexture, SubResourceIndex);
        commandList.CopyRegion(_pickWorldPositionRenderTarget, SubResourceIndex, textureRegion, _pickWorldPositionTexture, SubResourceIndex);
        commandList.CopyRegion(_pickWorldNormalRenderTarget, SubResourceIndex, textureRegion, _pickWorldNormalTexture, SubResourceIndex);

        // Get data
        _pickingHitTexture.GetData(commandList, _pickingHitTexture, pickingHitData);
        _pickWorldPositionTexture.GetData(commandList, _pickWorldPositionTexture, pickWorldPositionData);
        _pickWorldNormalTexture.GetData(commandList, _pickWorldNormalTexture, pickWorldNormalData);

        Debug.WriteLineIf(pickingHitData[0] > 0 && false, $"Pick Hit Pos: {pickWorldPositionData[0]} - Norm: {pickWorldNormalData[0]} - TexPos: ({textureX}, {textureY})");

        var pickHitResultData = new PaintingCursorHitResultData();
        if (pickingHitData[0] > 0)
        {
            pickHitResultData.PaintSessionId = _paintSessionId;
            pickHitResultData.IsHit = true;
            pickHitResultData.WorldPosition = pickWorldPositionData[0];
            pickHitResultData.WorldNormal = pickWorldNormalData[0];
        }
        return pickHitResultData;
    }
}
