using System.Collections.ObjectModel;
using System.Linq;
using BaseFramework.Core.Api;
using BaseFramework.Core.Scheduling;

namespace BaseFramework.Core.Tests;

public sealed class CalendarEventMutabilityTests
{
    [Fact]
    public void EditingOneEventDoesNotChangePeers()
    {
        var start = DateTime.Today.AddHours(9);
        var end = start.AddHours(2);

        var events = new ObservableCollection<TestCalendarEvent>
        {
            new() { Title = "One", Company = "A", Start = start, End = end },
            new() { Title = "Two", Company = "B", Start = start.AddHours(3), End = end.AddHours(3) },
            new() { Title = "Three", Company = "C", Start = start.AddDays(1), End = end.AddDays(1) }
        };

        var baseline = events.Select(e => (e.Start, e.End)).ToArray();

        events[0].Start = events[0].Start.AddHours(4);
        events[0].End = events[0].End.AddHours(4);

        Assert.NotEqual(baseline[0], (events[0].Start, events[0].End));
        Assert.Equal(baseline[1], (events[1].Start, events[1].End));
        Assert.Equal(baseline[2], (events[2].Start, events[2].End));
    }

    private sealed class TestCalendarEvent : ParameterBridgeObject, ICalendarEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();

        public string Title
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        public string Company
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        public string? Category
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        public string? Description
        {
            get => Get<string>() ?? string.Empty;
            set => Set(value);
        }

        public DateTime Start
        {
            get => Get<DateTime>();
            set => Set(value);
        }

        public DateTime End
        {
            get => Get<DateTime>();
            set => Set(value);
        }

        protected override void OnUpdate()
        {
        }
    }
}
