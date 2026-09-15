using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YMM4ShutterBlur;

internal sealed class ShutterBlurEffectProcessor : IVideoEffectProcessor
{
    const int MaximumExposureSamples = 8;
    const int MaximumSamplingInterval = 4;
    const int MaximumHistoryFrames =
        1 + (MaximumExposureSamples - 1) * MaximumSamplingInterval;
    const int MaximumTextureDimension = 16384;

    readonly DisposeCollector disposer = new();
    readonly IGraphicsDevicesAndContext devices;
    readonly ShutterBlurEffect item;
    readonly AffineTransform2D outputTransform;
    readonly LinkedList<FrameSnapshot> history = new();

    ID2D1Image? input;
    ID2D1CommandList? commandList;
    long lastInputFrame = long.MinValue;
    long heldGroup = long.MinValue;
    int heldHoldFrames;
    DrawDescription heldDrawDescription = null!;
    bool hasHeldOutput;

    public ID2D1Image Output { get; }

    public ShutterBlurEffectProcessor(
        IGraphicsDevicesAndContext devices,
        ShutterBlurEffect item)
    {
        this.devices = devices;
        this.item = item;

        outputTransform = new AffineTransform2D(devices.DeviceContext);
        disposer.Collect(outputTransform);

        Output = outputTransform.Output;
        disposer.Collect(Output);
    }

    public DrawDescription Update(EffectDescription description)
    {
        if (input is null)
        {
            outputTransform.SetInput(0, null, true);
            return description.DrawDescription;
        }

        var frame = description.ItemPosition.Frame;
        var length = description.ItemDuration.Frame;
        var fps = description.FPS;
        var sameFrame = frame == lastInputFrame;

        var exposureFrames = Math.Clamp(
            item.ExposureFrames.GetValue(frame, length, fps),
            1d,
            MaximumExposureSamples);
        var samplingInterval = Math.Clamp(
            (int)Math.Round(item.SamplingInterval.GetValue(frame, length, fps)),
            1,
            MaximumSamplingInterval);
        var linearFalloff = item.LinearFalloff.GetValue(frame, length, fps);
        var holdFrames = Math.Clamp(
            (int)Math.Round(item.HoldFrames.GetValue(frame, length, fps)),
            1,
            4);
        var mix = item.Mix.GetValue(frame, length, fps);

        if (lastInputFrame != long.MinValue && !sameFrame && frame != lastInputFrame + 1)
            ResetHistory();

        if (sameFrame && history.Last?.Value.Frame == frame)
        {
            history.Last.Value.Bitmap.Dispose();
            history.RemoveLast();
        }

        var snapshot = CaptureInput(frame);
        if (snapshot is not null)
            history.AddLast(snapshot);

        // よく使う1F間隔・8サンプル分は常に残す。
        // それ以上の間隔が必要になった場合だけ保持枚数を増やす。
        var requiredHistory = Math.Max(
            MaximumExposureSamples,
            TemporalWeights.GetRequiredHistoryLength(exposureFrames, samplingInterval));
        requiredHistory = Math.Min(requiredHistory, MaximumHistoryFrames);
        while (history.Count > requiredHistory)
        {
            history.First!.Value.Bitmap.Dispose();
            history.RemoveFirst();
        }

        var group = frame / holdFrames;

        var mustRender =
            !hasHeldOutput ||
            sameFrame ||
            holdFrames != heldHoldFrames ||
            group != heldGroup;

        if (mustRender)
        {
            RenderTemporalAverage(
                exposureFrames,
                samplingInterval,
                item.WeightCurve,
                linearFalloff,
                mix);
            heldGroup = group;
            heldHoldFrames = holdFrames;
            heldDrawDescription = description.DrawDescription;
            hasHeldOutput = true;
        }

        lastInputFrame = frame;
        return hasHeldOutput ? heldDrawDescription : description.DrawDescription;
    }

    FrameSnapshot? CaptureInput(long frame)
    {
        if (input is null)
            return null;

        var dc = devices.DeviceContext;
        var bounds = dc.GetImageLocalBounds(input);
        var width = Math.Min(
            (int)Math.Ceiling(bounds.Right - bounds.Left),
            MaximumTextureDimension);
        var height = Math.Min(
            (int)Math.Ceiling(bounds.Bottom - bounds.Top),
            MaximumTextureDimension);

        if (width <= 0 || height <= 0)
            return null;

        var properties = new BitmapProperties1(
            new(
                Vortice.DXGI.Format.B8G8R8A8_UNorm,
                Vortice.DCommon.AlphaMode.Premultiplied),
            96,
            96,
            BitmapOptions.Target);
        var bitmap = dc.CreateBitmap(
            new Vortice.Mathematics.SizeI(width, height),
            properties);

        dc.Target = bitmap;
        dc.BeginDraw();
        dc.Clear(null);
        dc.Transform = Matrix3x2.CreateTranslation(-bounds.Left, -bounds.Top);
        dc.DrawImage(input);
        dc.Transform = Matrix3x2.Identity;
        dc.EndDraw();
        dc.Target = null;

        return new FrameSnapshot(bitmap, frame);
    }

    void RenderTemporalAverage(
        double exposureFrames,
        int samplingInterval,
        TemporalWeightCurve curve,
        double linearFalloff,
        double mix)
    {
        if (history.Count == 0)
        {
            outputTransform.SetInput(0, null, true);
            return;
        }

        outputTransform.SetInput(0, null, true);
        disposer.RemoveAndDispose(ref commandList);

        var frames = history
            .Reverse()
            .Where((_, index) => index % samplingInterval == 0)
            .Take(TemporalWeights.GetRequestedSampleCount(exposureFrames))
            .ToArray();
        var weights = TemporalWeights.Create(
            frames.Length,
            exposureFrames,
            curve,
            linearFalloff,
            mix);

        var dc = devices.DeviceContext;
        commandList = dc.CreateCommandList();
        disposer.Collect(commandList);

        dc.Target = commandList;
        dc.BeginDraw();
        dc.Clear(null);
        dc.PrimitiveBlend = PrimitiveBlend.Add;

        for (var i = 0; i < frames.Length; i++)
            dc.DrawBitmap(frames[i].Bitmap, weights[i], BitmapInterpolationMode.Linear);

        dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
        dc.EndDraw();
        dc.Target = null;
        commandList.Close();

        var bounds = dc.GetImageLocalBounds(commandList);
        outputTransform.TransformMatrix = Matrix3x2.CreateTranslation(
            -(bounds.Left + bounds.Right) / 2f,
            -(bounds.Top + bounds.Bottom) / 2f);
        outputTransform.SetInput(0, commandList, true);
    }

    public void SetInput(ID2D1Image? value) => input = value;

    public void ClearInput() => input = null;

    void ResetHistory()
    {
        while (history.First is not null)
        {
            history.First.Value.Bitmap.Dispose();
            history.RemoveFirst();
        }

        outputTransform.SetInput(0, null, true);
        disposer.RemoveAndDispose(ref commandList);
        hasHeldOutput = false;
        heldGroup = long.MinValue;
    }

    public void Dispose()
    {
        ResetHistory();
        outputTransform.SetInput(0, null, true);
        disposer.Dispose();
    }

    sealed record FrameSnapshot(ID2D1Bitmap1 Bitmap, long Frame);
}
