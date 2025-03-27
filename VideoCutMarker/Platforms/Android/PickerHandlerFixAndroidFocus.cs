using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace VideoCutMarker.Platforms.AndroidModule
{
	public class PickerHandlerFixAndroidFocus : PickerHandler
	{
		public PickerHandlerFixAndroidFocus()
		{
			_onFocusChangeMethod = typeof(PickerHandler).GetMethod("OnFocusChange", BindingFlags.Instance | BindingFlags.NonPublic);
			_onClickMethod = typeof(PickerHandler).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);

			System.Diagnostics.Debug.Assert(_onFocusChangeMethod != null && _onClickMethod != null);
		}

		protected override void ConnectHandler(MauiPicker platformView)
		{
			base.ConnectHandler(platformView);

			var focusChangeDelegate = (System.EventHandler<Android.Views.View.FocusChangeEventArgs>)Delegate.CreateDelegate(typeof(System.EventHandler<Android.Views.View.FocusChangeEventArgs>), this, _onFocusChangeMethod);
			var clickDelegate = (System.EventHandler)Delegate.CreateDelegate(typeof(System.EventHandler), this, _onClickMethod);

			platformView.Click -= clickDelegate;
			platformView.FocusChange -= focusChangeDelegate;

			platformView.Click += OnClick;
			platformView.FocusChange += OnFocusChange;
		}

		void OnClick(object? sender, EventArgs e)
		{
			var diff = DateTime.Now - _lastFocusTimeStamp;

			if (diff <= _timeToIgnoreClickAfterFocus)
			{
				_lastFocusTimeStamp = DateTime.MinValue;
				return;
			}

			_onClickMethod!.Invoke(this, [sender, e]);
		}

		void OnFocusChange(object? sender, global::Android.Views.View.FocusChangeEventArgs e)
		{
			if (e.HasFocus)
			{
				_lastFocusTimeStamp = DateTime.Now;
				return;
			}

			_lastFocusTimeStamp = DateTime.MinValue;

			_onFocusChangeMethod!.Invoke(this, [sender, e]);
		}

		DateTime _lastFocusTimeStamp = DateTime.MinValue;
		MethodInfo? _onClickMethod;
		MethodInfo? _onFocusChangeMethod;

		readonly TimeSpan _timeToIgnoreClickAfterFocus = TimeSpan.FromSeconds(0.1f);
	}
}
