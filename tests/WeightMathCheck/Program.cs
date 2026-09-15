using YMM4ShutterBlur;

static void AssertClose(float expected, float actual, string message)
{
    if (MathF.Abs(expected - actual) > 0.0001f)
        throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
}

foreach (var curve in Enum.GetValues<TemporalWeightCurve>())
{
    foreach (var exposure in new[] { 1d, 2d, 2.5d, 3d, 3.5d, 8d })
    {
        foreach (var falloff in new[] { 0d, 50d, 100d })
        {
            foreach (var mix in new[] { 0d, 25d, 50d, 70d, 100d })
            {
                var weights = TemporalWeights.Create(8, exposure, curve, falloff, mix);
                AssertClose(1f, weights.Sum(), $"sum ({curve}, {exposure}, {falloff}, {mix})");

                if (weights.Any(weight => weight < 0f || weight > 1f))
                    throw new InvalidOperationException($"invalid range ({curve}, {exposure}, {falloff}, {mix})");
            }
        }
    }
}

var equal = TemporalWeights.Create(4, 4, TemporalWeightCurve.Equal, 100, 100);
foreach (var weight in equal)
    AssertClose(0.25f, weight, "equal 4-frame weight");

var linear = TemporalWeights.Create(4, 4, TemporalWeightCurve.Linear, 100, 100);
AssertClose(0.4f, linear[0], "linear newest");
AssertClose(0.3f, linear[1], "linear second");
AssertClose(0.2f, linear[2], "linear third");
AssertClose(0.1f, linear[3], "linear oldest");

var fractionalEqual = TemporalWeights.Create(4, 3.5, TemporalWeightCurve.Equal, 100, 100);
AssertClose(1f / 3.5f, fractionalEqual[0], "fractional equal newest");
AssertClose(1f / 3.5f, fractionalEqual[1], "fractional equal second");
AssertClose(1f / 3.5f, fractionalEqual[2], "fractional equal third");
AssertClose(0.5f / 3.5f, fractionalEqual[3], "fractional equal oldest");

var softenedLinear = TemporalWeights.Create(4, 4, TemporalWeightCurve.Linear, 50, 100);
if (!(softenedLinear[0] > softenedLinear[1] && softenedLinear[1] > softenedLinear[2]))
    throw new InvalidOperationException("softened linear curve is not descending");

if (TemporalWeights.GetRequestedSampleCount(3.5) != 4)
    throw new InvalidOperationException("fractional exposure sample count is incorrect");

if (TemporalWeights.GetRequiredHistoryLength(4, 2) != 7)
    throw new InvalidOperationException("2-frame sampling history length is incorrect");

if (TemporalWeights.GetRequiredHistoryLength(8, 4) != 29)
    throw new InvalidOperationException("maximum history length is incorrect");

Console.WriteLine("Temporal weight checks passed.");
