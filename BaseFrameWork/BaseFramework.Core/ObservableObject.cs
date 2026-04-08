using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BaseFramework.Core.UndoRedo;

namespace BaseFramework.Core;

public abstract class ObservableObject : INotifyPropertyChanged
{
    private readonly HashSet<ObservableObject> _dependents = new();
    private readonly HashSet<ObservableObject> _dependencies = new();
    private readonly HashSet<string> _rejectedKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object?> _propertyBag = new(StringComparer.OrdinalIgnoreCase);
    private bool _isUpdating;

    protected ObservableObject(UndoRedoManager? undoRedoManager = null)
    {
        UndoRedoManager = undoRedoManager;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LayoutInvalidated;

    public bool IsDirty { get; private set; } = true;

    protected UndoRedoManager? UndoRedoManager { get; }

    public void AddDependency(ObservableObject dependency)
    {
        if (_dependencies.Add(dependency))
        {
            dependency._dependents.Add(this);
            MarkDirty();
        }
    }

    protected void AddRejection(string key)
    {
        if (_rejectedKeys.Add(key))
        {
            OnLayoutInvalidated();
        }
    }

    protected void RemoveRejection(string key)
    {
        if (_rejectedKeys.Remove(key))
        {
            OnLayoutInvalidated();
        }
    }

    public bool IsRejected(string key) => _rejectedKeys.Contains(key);

    public void Update()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            foreach (var dependency in _dependencies)
            {
                dependency.Update();
            }

            if (!IsDirty)
            {
                return;
            }

            OnUpdate();
            IsDirty = false;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public T Get<T>([CallerMemberName] string propertyName = "")
    {
        Update();
        if (_propertyBag.TryGetValue(propertyName, out var value) && value is T typed)
        {
            return typed;
        }

        return default!;
    }

    public void Set<T>(T value, [CallerMemberName] string propertyName = "")
    {
        _propertyBag.TryGetValue(propertyName, out var previous);
        if (Equals(previous, value))
        {
            return;
        }

        _propertyBag[propertyName] = value;
        MarkDirty();
        Update();
        UndoRedoManager?.Push(new PropertyChangeAction(this, propertyName, previous, value));
        OnPropertyChanged(propertyName);
        NotifyDependents();
    }

    public object? GetRaw(string propertyName)
    {
        Update();
        return _propertyBag.TryGetValue(propertyName, out var value) ? value : null;
    }

    public void ApplyExternalValue(string propertyName, object? value)
    {
        _propertyBag[propertyName] = value;
        MarkDirty();
        Update();
        OnPropertyChanged(propertyName);
        NotifyDependents();
    }

    protected void MarkDirty()
    {
        IsDirty = true;
        foreach (var dependent in _dependents)
        {
            dependent.MarkDirty();
        }
    }

    private void NotifyDependents()
    {
        if (_dependents.Count == 0)
        {
            return;
        }

        foreach (var dependent in _dependents)
        {
            dependent.Update();
        }
    }

    protected abstract void OnUpdate();

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected virtual void OnLayoutInvalidated()
        => LayoutInvalidated?.Invoke(this, EventArgs.Empty);
}
