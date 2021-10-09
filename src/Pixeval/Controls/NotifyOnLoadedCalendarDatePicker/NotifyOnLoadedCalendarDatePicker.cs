﻿using Windows.Foundation;
using Microsoft.UI.Xaml.Controls;

namespace Pixeval.Controls.NotifyOnLoadedCalendarDatePicker
{
    public class NotifyOnLoadedCalendarDatePicker : CalendarDatePicker
    {
        public NotifyOnLoadedCalendarDatePicker()
        {
            DefaultStyleKey = typeof(NotifyOnLoadedCalendarDatePicker);
            DateChanged += (sender, args) =>
            {
                if (IsLoaded)
                {
                    _dateChangedWhenLoaded?.Invoke(sender, args);
                }
            };
        }

        private TypedEventHandler<CalendarDatePicker, CalendarDatePickerDateChangedEventArgs>? _dateChangedWhenLoaded;

        public event TypedEventHandler<CalendarDatePicker, CalendarDatePickerDateChangedEventArgs> DateChangedWhenLoaded
        {
            add => _dateChangedWhenLoaded += value;
            remove => _dateChangedWhenLoaded -= value;
        }
    }
}