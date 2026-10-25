using System.Globalization;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Options;

namespace BBH.CP.Atlas.Service.Application.TTDV2
{
    /// <summary>Tax years: next year, this year and the 3 years before (as TTD). The JSON default is {Options[1]} (this year).</summary>
    public sealed class TaxYearsOptionProvider(TimeProvider time) : IAtlasOptionProvider
    {
        public string Name => "TaxYears";

        public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request)
        {
            int year = time.GetLocalNow().Year;
            return [.. Enumerable.Range(0, 5)
                .Select(n => (year + 1 - n).ToString(CultureInfo.InvariantCulture))
                .Select(x => new CPKeyValue { Label = x, Value = x })];
        }
    }

    /// <summary>
    /// Week endings: the last Sunday (today if today is Sunday) and the next 3 Sundays, as yyyy-MM-dd.
    /// The JSON default is {Options[0]}. Finding B6: the TTD loop adds the same Sunday 3 times.
    /// </summary>
    public sealed class WeekEndingsOptionProvider(TimeProvider time) : IAtlasOptionProvider
    {
        public string Name => "WeekEndings";

        public IReadOnlyList<CPKeyValue> GetOptions(AtlasOptionRequest request)
        {
            DateTime today = time.GetLocalNow().Date;
            DateTime lastSunday = today.AddDays(-(int)today.DayOfWeek);
            return [.. Enumerable.Range(0, 4)
                .Select(n => lastSunday.AddDays(7 * n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Select(x => new CPKeyValue { Label = x, Value = x })];
        }
    }
}
