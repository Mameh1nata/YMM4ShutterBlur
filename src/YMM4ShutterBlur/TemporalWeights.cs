namespace YMM4ShutterBlur;

internal static class TemporalWeights
{
    public static float[] Create(
        int availableCount,
        double exposureFrames,
        TemporalWeightCurve curve,
        double linearFalloffPercent,
        double mixPercent)
    {
        availableCount = Math.Clamp(availableCount, 1, 8);
        exposureFrames = Math.Clamp(exposureFrames, 1d, 8d);
        var requestedCount = GetRequestedSampleCount(exposureFrames);
        var count = Math.Min(availableCount, requestedCount);
        var falloff = (float)Math.Clamp(linearFalloffPercent / 100d, 0d, 1d);
        var mix = (float)Math.Clamp(mixPercent / 100d, 0d, 1d);
        var weights = new float[count];

        var fullSamples = (int)Math.Floor(exposureFrames);
        var fractionalSample = (float)(exposureFrames - fullSamples);

        var rawSum = 0f;
        for (var i = 0; i < count; i++)
        {
            // 均等カーブでは、小数部分を最古サンプルの占有率として扱う。
            var equalRaw = i < fullSamples ? 1f : fractionalSample;

            // 旧版の整数値（4Fなら4:3:2:1）と同じ比率を維持しつつ、
            // 露光量を小数で動かした場合にも最古サンプルが連続的に増減する。
            var linearRaw = Math.Max(0f, 1f - i / (float)exposureFrames);
            var raw = curve == TemporalWeightCurve.Linear
                ? equalRaw + (linearRaw - equalRaw) * falloff
                : equalRaw;

            weights[i] = raw;
            rawSum += raw;
        }

        if (rawSum <= float.Epsilon)
        {
            weights[0] = 1f;
            return weights;
        }

        for (var i = 0; i < count; i++)
            weights[i] = mix * weights[i] / rawSum;

        // 「ミックス」を下げた分は最新フレームへ戻す。
        // 合計は常に1.0なので、静止部分の明るさが変化しない。
        weights[0] += 1f - mix;
        return weights;
    }

    public static int GetRequestedSampleCount(double exposureFrames) =>
        Math.Clamp((int)Math.Ceiling(Math.Clamp(exposureFrames, 1d, 8d)), 1, 8);

    public static int GetRequiredHistoryLength(double exposureFrames, int samplingInterval)
    {
        samplingInterval = Math.Clamp(samplingInterval, 1, 4);
        return 1 + (GetRequestedSampleCount(exposureFrames) - 1) * samplingInterval;
    }
}
