// STAND-IN: do not copy to the target.
// Target: Data/Data/DTO/CPAtlasDashboardConfig.cs. Same classes and properties; the classes are "partial"
// (INTEGRATION.md). The rule extension methods are not copied: the platform uses AtlasRuleEngine (module 4).
using BBH.CP.Atlas.Data.Enumerations;

namespace BBH.CP.Atlas.Data.DTO
{
    public partial class CPAtlasDashboardConfig
    {
        public List<CPAtlasDashboard>? Dashboards { get; set; }
    }

    public partial class CPAtlasDashboard
    {
        public CPApplicationCode Code { get; set; }
        public string? Name { get; set; }
        public List<CPAtlasTab>? Tabs { get; set; }
        public List<CPButton>? Buttons { get; set; }
    }

    public partial class CPAtlasTab
    {
        public string? ID { get; set; }
        public string? Code { get; set; }
        public string? Label { get; set; }
        public string? Description { get; set; }
        public bool Visible { get; set; } = true;
        public string? Procedure { get; set; }
        public List<CPParameter>? Parameters { get; set; }
        public List<Dictionary<string, object>>? KeyValues { get; set; }
        public Dictionary<string, int>? KeyLength { get; set; }
        public List<string>? Groups { get; set; }
        public List<string>? Sums { get; set; }
        public Dictionary<string, List<string>>? ColumnTypes { get; set; }
        public List<CPMenu>? Menus { get; set; }
        public List<CPRule>? Rules { get; set; }
    }

    public partial class CPParameter
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Type { get; set; }
        public int? Length { get; set; }
    }

    public partial class CPMenu
    {
        public string? Name { get; set; }
        public CPRule? Rule { get; set; }
        public bool Visible { get; set; } = true;
    }
}
