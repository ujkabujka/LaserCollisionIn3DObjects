using System;
using System.ComponentModel;

namespace BaseFramework.Core.Scheduling;

public interface ICalendarEvent : INotifyPropertyChanged
{
    Guid EventId { get; }
    string Title { get; }
    string Company { get; }
    string? Category { get; }
    string? Description { get; }
    DateTime Start { get; }
    DateTime End { get; }
}
