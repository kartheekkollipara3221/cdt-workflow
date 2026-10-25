using System.Globalization;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Application.TTDV2;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Sections;

namespace BBH.CP.Atlas.Service.Application
{
    /// <summary>
    /// TTDV2 (spec 12, section 3). Everything else is in Config/TTDV2.json. The module adds two option providers
    /// and one hook: the grid column headers show the dates of the selected week (spec 01, 10.1).
    /// </summary>
    public class CPTTDV2Service(AtlasModuleServices services) : ConfigSectionService(services)
    {
        protected override IEnumerable<IAtlasOptionProvider> AppOptionProviders =>
            [new TaxYearsOptionProvider(Services.Time), new WeekEndingsOptionProvider(Services.Time)];

        protected override void OnLoad(AtlasLoadContext context)
        {
            CPApplicationControl? weekEnding = context.Section.FindControlByName("WeekEnding");
            CPApplicationControl? weeklyHour = context.Section.FindControlByName("WeeklyHour");
            if (weeklyHour?.Grid?.Columns == null
                || !DateTime.TryParseExact(weekEnding?.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                return;

            // The first 7 columns are Monday to Sunday; the last column is the total.
            for (int n = 0; n < Math.Min(7, weeklyHour.Grid.Columns.Count); n++)
                weeklyHour.Grid.Columns[n].headerName = date.AddDays(-6 + n).ToString("dddd dd-MMM-yyyy", CultureInfo.InvariantCulture);
        }
    }
}
