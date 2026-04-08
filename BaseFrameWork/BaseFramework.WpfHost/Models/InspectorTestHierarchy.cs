using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using BaseFramework.Core.Api;
using BaseFramework.Core.Attributes;
using BaseFramework.Core.Notes;
using BaseFramework.Core.Scheduling;

namespace BaseFramework.WpfHost.Models;

[GenerateInspectorMetadata]
public class Test_Class_1 : ParameterBridgeObject
{
    [InspectableMember("test.name", "Name", Order = 1)]
    public string Name
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("test.value1", "Value 1", Order = 2)]
    public double Value1
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("test.value2", "Value 2", Order = 3)]
    public double Value2
    {
        get => Get<double>();
        set => Set(value);
    }

    protected override void OnUpdate()
    {
    }
}

[GenerateInspectorMetadata]
public class Test_Class_1_A : Test_Class_1
{
    [InspectableMember("test.a.mode", "Operating Mode", Order = 11)]
    public string Mode
    {
        get => Get<string>() ?? "Standard";
        set => Set(value);
    }
}

[GenerateInspectorMetadata]
public class Test_Class_1_B : Test_Class_1
{
    [InspectableMember("test.b.multiplier", "Multiplier", Order = 11)]
    public double Multiplier
    {
        get => Get<double>();
        set => Set(value);
    }
}

[GenerateInspectorMetadata]
public class Test_Class_2 : Test_Class_1, ICalendarEvent
{
    protected static readonly IReadOnlyList<string> DefaultEngagementTags = new[] { "Discovery", "Design", "Delivery", "QA" };
    public Guid EventId { get; } = Guid.NewGuid();

    [InspectableMember("test.value3", "Value 3", Order = 4)]
    public double Value3
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("test.value4", "Value 4", Order = 5)]
    public double Value4
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("test.company", "Company", Order = 6)]
    public string Company
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("test.engagementTag", "Engagement Tag", Order = 7, ValueSourcePropertyName = nameof(EngagementTags))]
    public string EngagementTag
    {
        get => Get<string>() ?? EngagementTags.First();
        set => Set(value);
    }

    public IEnumerable<string> EngagementTags => DefaultEngagementTags;

    [InspectableMember("test.project", "Project", Order = 8)]
    public string Project
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    [InspectableMember("test.start", "Start", Order = 9)]
    public DateTime StartDate
    {
        get => Get<DateTime>();
        set => Set(value);
    }

    [InspectableMember("test.end", "End", Order = 10)]
    public DateTime EndDate
    {
        get => Get<DateTime>();
        set => Set(value);
    }

    [InspectableMember("test.description", "Description", Order = 11)]
    public string Description
    {
        get => Get<string>() ?? string.Empty;
        set => Set(value);
    }

    public string Title => Name;
    string? ICalendarEvent.Category => Project;
    string? ICalendarEvent.Description => Description;
    DateTime ICalendarEvent.Start => StartDate;
    DateTime ICalendarEvent.End => EndDate;

    protected override void OnUpdate()
    {
        base.OnUpdate();

        var start = Convert.ToDateTime(GetRaw(nameof(StartDate)) ?? DateTime.Now);
        var end = Convert.ToDateTime(GetRaw(nameof(EndDate)) ?? start);
        if (end <= start)
        {
            Set(start.AddHours(1), nameof(EndDate));
        }
    }
}

[GenerateInspectorMetadata]
public class Test_Class_3 : Test_Class_2
{
    private ObservableCollection<Test_Class_2>? _scheduleCache;
    private static readonly IReadOnlyList<string> PreferredVendors = new[] { "Acme", "Northwind", "Contoso", "AdventureWorks" };

    public Test_Class_3()
    {
        Name = "Main Test";
        Value1 = 10;
        Value2 = 20;
        Value3 = 30;
        Value4 = 60;
        EngagementTag = DefaultEngagementTags[0];
        PreferredVendor = PreferredVendors[0];
        NestedClass = new Test_Class_1_A { Name = "Nested", Value1 = 1.5, Value2 = 2.5 };
        AddDependency(NestedClass);
        ShowNestedClass = true;
        SeedSchedule(ScheduledItems);
        TeamNotes = new NoteDocument("Başlangıç notları burada.");
    }

    [InspectableMember("test.extra", "Extra Value", Order = 20)]
    public double ExtraValue
    {
        get => Get<double>();
        set => Set(value);
    }

    [InspectableMember("test.vendor", "Preferred Vendor", Order = 19, ValueSourcePropertyName = nameof(VendorOptions))]
    public string PreferredVendor
    {
        get => Get<string>() ?? PreferredVendors[0];
        set => Set(value);
    }

    public IEnumerable<string> VendorOptions => PreferredVendors;

    [InspectableMember("test.showNested", "Show Nested Class", Order = 21)]
    public bool ShowNestedClass
    {
        get => Get<bool>();
        set
        {
            Set(value);
            if (value)
            {
                RemoveRejection("test.class1");
            }
            else
            {
                AddRejection("test.class1");
            }
        }
    }

    [InspectableMember("test.class1", "Nested Test Class 1", Order = 22)]
    public Test_Class_1 NestedClass
    {
        get => Get<Test_Class_1>();
        set
        {
            Set(value);
            AddDependency(value);
        }
    }

    [InspectableMember("test.sum", "Calculated Sum", ReadOnly = true, Order = 23)]
    public double CalculatedSum
    {
        get => Get<double>();
        private set => Set(value);
    }

    [InspectableMember("test.noparam", "Trigger Without Parameters", Order = 24)]
    public void TriggerWithoutParameters()
    {
        CalculatedSum = Value1 + Value2;
    }

    [InspectableMember("test.twodouble", "Sum With Two Doubles", Order = 25)]
    public void SumTwoDoubles(double left = 12.25, double right = 7.75)
    {
        CalculatedSum = left + right;
    }

    [InspectableMember("test.schedule", "Work Items", Order = 30)]
    public ObservableCollection<Test_Class_2> ScheduledItems
    {
        get => _scheduleCache ??= InitializeScheduleCollection();
        set => ReplaceScheduleCollection(value);
    }

    [InspectableMember("test.schedule.add", "Add Work Item", Order = 31)]
    public void AddScheduleItem()
    {
        var index = ScheduledItems.Count + 1;
        var company = (index % 3) switch
        {
            0 => "Acme",
            1 => "Northwind",
            _ => "Contoso"
        };

        var start = DateTime.Today.AddDays(index);
        var entry = CreateScheduleEntry($"Planned Task {index}", company, start, TimeSpan.FromHours(2 + index % 3), $"Auto-generated item #{index}.");
        ScheduledItems.Add(entry);
    }

    [InspectableMember("test.notes", "Team Notes", Order = 32)]
    public NoteDocument TeamNotes
    {
        get => Get<NoteDocument>() ?? new NoteDocument();
        set => Set(value ?? new NoteDocument());
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        var value3 = Convert.ToDouble(GetRaw(nameof(Value3)) ?? 0d);
        var expectedValue4 = value3 * 2d;
        var currentValue4 = Convert.ToDouble(GetRaw(nameof(Value4)) ?? 0d);
        if (!currentValue4.Equals(expectedValue4))
        {
            Set(expectedValue4, nameof(Value4));
        }

        var nested = GetRaw(nameof(NestedClass)) as Test_Class_1;
        if (nested is null)
        {
            return;
        }

        var nestedValue1 = Convert.ToDouble(nested.GetRaw(nameof(Test_Class_1.Value1)) ?? 0d);
        var expectedValue2 = nestedValue1 * 3d;
        var currentValue2 = Convert.ToDouble(GetRaw(nameof(Value2)) ?? 0d);
        if (!currentValue2.Equals(expectedValue2))
        {
            Set(expectedValue2, nameof(Value2));
        }
    }

    private ObservableCollection<Test_Class_2> InitializeScheduleCollection()
    {
        var collection = new ObservableCollection<Test_Class_2>();
        AttachScheduleCollection(collection);
        Set(collection, nameof(ScheduledItems));
        _scheduleCache = collection;
        return collection;
    }

    private void ReplaceScheduleCollection(ObservableCollection<Test_Class_2> collection)
    {
        if (ReferenceEquals(_scheduleCache, collection))
        {
            return;
        }

        if (_scheduleCache is not null)
        {
            _scheduleCache.CollectionChanged -= HandleScheduleCollectionChanged;
        }

        _scheduleCache = collection;
        Set(collection, nameof(ScheduledItems));
        AttachScheduleCollection(collection);
        OnLayoutInvalidated();
    }

    private void AttachScheduleCollection(ObservableCollection<Test_Class_2> collection)
    {
        collection.CollectionChanged += HandleScheduleCollectionChanged;
        foreach (var item in collection)
        {
            AddDependency(item);
        }
    }

    private void HandleScheduleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<Test_Class_2>())
            {
                AddDependency(item);
            }
        }

        OnLayoutInvalidated();
    }

    private void SeedSchedule(ICollection<Test_Class_2> schedule)
    {
        schedule.Clear();
        var today = DateTime.Today;

        schedule.Add(CreateScheduleEntry("Acme Kickoff", "Acme", today.AddHours(9), TimeSpan.FromHours(2), "Initial planning with Acme stakeholders."));
        schedule.Add(CreateScheduleEntry("Northwind Design", "Northwind", today.AddDays(1).AddHours(11), TimeSpan.FromHours(3), "UX review for Northwind dashboards."));
        schedule.Add(CreateScheduleEntry("Contoso QA", "Contoso", today.AddDays(2).AddHours(14), TimeSpan.FromHours(4), "Regression testing support."));
        schedule.Add(CreateScheduleEntry("Acme Sprint Review", "Acme", today.AddDays(4).AddHours(10), TimeSpan.FromHours(1.5), "Sprint review & demo."));
        schedule.Add(CreateScheduleEntry("Northwind Deployment", "Northwind", today.AddDays(6).AddHours(13), TimeSpan.FromHours(2.5), "Production deployment assistance."));
    }

    private static Test_Class_2 CreateScheduleEntry(string name, string company, DateTime start, TimeSpan duration, string description)
    {
        return new Test_Class_2
        {
            Name = name,
            Company = company,
            Project = $"{company} Initiative",
            StartDate = start,
            EndDate = start.Add(duration),
            Description = description,
            Value3 = start.Day,
            Value4 = start.Day * 2
        };
    }
}
