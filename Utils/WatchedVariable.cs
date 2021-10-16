// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;

namespace WatchedVariable
{
    public class WatchedBool : WatchedVariable<bool>
    {
        public WatchedBool(bool initialValue = false) : base(initialValue) { }
    }

    public interface IBindable<T>
    {
        void BindWith(T other);
        void UnBindWith(T other);
    }

    [System.Diagnostics.DebuggerDisplay("{internal_value}")]
    public class WatchedVariable<T> : IBindable<WatchedVariable<T>>
    {
        public event Action Changed;

        private T internal_value;
        private Action<T> OnValueChanged { get; set; }
        private Action OnChanged { get; set; }

        public WatchedVariable(T initialValue = default(T))
        {
            internal_value = initialValue;
        }

        public WatchedVariable(Action<T> onChanged)
        {
            internal_value = default(T);
            OnValueChanged = onChanged;
        }

        public WatchedVariable(Action onChanged)
        {
            internal_value = default(T);
            OnChanged = onChanged;
        }

        public WatchedVariable(T initialValue, Action<T> onChanged)
        {
            internal_value = initialValue;
            OnValueChanged = onChanged;
        }

        public WatchedVariable(T initialValue, Action onChanged)
        {
            internal_value = initialValue;
            OnChanged = onChanged;
        }

        public void Set(T value)
        {
            Value = value;
        }

        // Useful if you cannot set the action at construction.
        public void Set(T value, Action<T> onValueChanged)
        {
            OnValueChanged = onValueChanged;
            Value = value;
        }

        // Useful if you cannot set the action at construction.
        public void Set(T value, Action onChanged)
        {
            OnChanged = onChanged;
            Value = value;
        }

        public T Value
        {
            get => internal_value;
            set
            {
                if ( (internal_value == null && value != null) 
                    || (internal_value != null && !internal_value.Equals(value)))
                {
                    internal_value = value;
                    EmitValueChangeEvents();
                }
            }
        }

        private void EmitValueChangeEvents()
        {
            OnValueChanged?.Invoke(internal_value);
            OnChanged?.Invoke();
            Changed?.Invoke();

            EmitToBoundVariables();
        }

        public static implicit operator T(WatchedVariable<T> obj)
        {
            return obj.Value;
        }

        #region Binding

        //
        // Variables can be bound so that any change to one affects the others.
        // Useful to reduce combinatorial complexity for UI events.
        //

        public void BindWith(WatchedVariable<T> other)
        {
            // TODO: implement with weak refs
            this.Value = other.Value;
            boundVariables.Add(other);
            other.boundVariables.Add(this);
        }

        public void UnBindWith(WatchedVariable<T> other)
        {
            boundVariables.Remove(other);
            other.boundVariables.Remove(this);
        }

        private HashSet<WatchedVariable<T>> boundVariables = new();

        private bool currentlyEmitting;
        private void EmitToBoundVariables()
        {
            if (!currentlyEmitting)
            {
                currentlyEmitting = true;
                foreach (var boundvar in boundVariables)
                {
                    boundvar.internal_value = internal_value;
                    boundvar.EmitValueChangeEvents();
                }
                currentlyEmitting = false;
            }
        }
        
        #endregion //Binding
    }
}
