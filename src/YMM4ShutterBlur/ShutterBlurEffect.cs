using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YMM4ShutterBlur;

[VideoEffect(
    "シャッターブラー",
    ["フィルター"],
    ["モーションブラー", "フレームブレンド", "temporal blur", "shutter blur"],
    isAviUtlSupported: false)]
internal sealed class ShutterBlurEffect : VideoEffectBase
{
    public override string Label => "シャッターブラー";

    [Display(
        GroupName = "シャッターブラー",
        Name = "露光フレーム",
        Description = "時間方向に混ぜるサンプル数。2.5Fのような小数も指定できます。",
        Order = 10)]
    [AnimationSlider("F1", "F", 1, 8)]
    public Animation ExposureFrames { get; } = new(4, 1, 8);

    [Display(
        GroupName = "シャッターブラー",
        Name = "サンプリング間隔",
        Description = "履歴を拾う間隔。A A B Bのような重複30fps素材を60fpsで扱う場合は2Fです。",
        Order = 20)]
    [AnimationSlider("F0", "F", 1, 4)]
    public Animation SamplingInterval { get; } = new(1, 1, 4);

    [Display(
        GroupName = "シャッターブラー",
        Name = "重み",
        Description = "均等は全フレームを同じ濃さで混ぜ、線形は古いフレームほど薄くします。",
        Order = 30)]
    [EnumComboBox]
    public TemporalWeightCurve WeightCurve
    {
        get => weightCurve;
        set => Set(ref weightCurve, value);
    }
    TemporalWeightCurve weightCurve = TemporalWeightCurve.Linear;

    [Display(
        GroupName = "シャッターブラー",
        Name = "線形減衰",
        Description = "重みが「線形」のときに、古いサンプルを薄くする強さ。0%で均等、100%で完全な線形減衰です。",
        Order = 40)]
    [AnimationSlider("F0", "%", 0, 100)]
    public Animation LinearFalloff { get; } = new(100, 0, 100);

    [Display(
        GroupName = "シャッターブラー",
        Name = "ミックス",
        Description = "100%で完全な時間平均。下げるほど現在フレームを強く残します。",
        Order = 50)]
    [AnimationSlider("F0", "%", 0, 100)]
    public Animation Mix { get; } = new(70, 0, 100);

    [Display(
        GroupName = "シャッターブラー",
        Name = "フレーム保持",
        Description = "同じ出力を保持するフレーム数。60fpsプロジェクトを30fps風にする場合は2です。",
        Order = 60)]
    [AnimationSlider("F0", "F", 1, 4)]
    public Animation HoldFrames { get; } = new(2, 1, 4);

    public override IEnumerable<string> CreateExoVideoFilters(
        int keyFrameIndex,
        ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) =>
        new ShutterBlurEffectProcessor(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [ExposureFrames, SamplingInterval, LinearFalloff, Mix, HoldFrames];
}
