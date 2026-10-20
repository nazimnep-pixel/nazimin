namespace AzMepCad.AutoCAD.Services;

using AzMepCad.Core.Interfaces;

/// <summary>
/// Future bridge between drawing entities and <see cref="IMepComponent"/>.
/// </summary>
public interface IDrawingMepBridge
{
    IReadOnlyList<IMepComponent> ScanCurrentDrawing();
}

#if AZMEP_NO_AUTOCAD_SDK

public sealed class DrawingMepBridgeStub : IDrawingMepBridge
{
    public IReadOnlyList<IMepComponent> ScanCurrentDrawing() => Array.Empty<IMepComponent>();
}

#else

public sealed class DrawingMepBridgeStub : IDrawingMepBridge
{
    public IReadOnlyList<IMepComponent> ScanCurrentDrawing() => Array.Empty<IMepComponent>();
}

#endif
