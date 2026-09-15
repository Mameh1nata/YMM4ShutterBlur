using System.ComponentModel.DataAnnotations;

namespace YMM4ShutterBlur;

internal enum TemporalWeightCurve
{
    [Display(Name = "均等")]
    Equal,

    [Display(Name = "線形（古いほど薄い）")]
    Linear,
}
