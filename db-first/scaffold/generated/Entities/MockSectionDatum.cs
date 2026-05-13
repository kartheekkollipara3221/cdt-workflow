using System;
using System.Collections.Generic;

namespace WorkflowEngine.DatabaseFirst.Generated.Entities;

public partial class MockSectionDatum
{
    public long Id { get; set; }

    public string BusinessObjectType { get; set; } = null!;

    public string BusinessObjectId { get; set; } = null!;

    public string SectionKey { get; set; } = null!;

    public string PayloadJson { get; set; } = null!;

    public string SavedByUserId { get; set; } = null!;

    public DateTimeOffset SavedAtUtc { get; set; }

    public string CorrelationId { get; set; } = null!;
}
