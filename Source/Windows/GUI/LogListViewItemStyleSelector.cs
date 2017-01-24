using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace MAPE.Windows.GUI {
	public class LogListViewItemStyleSelector: StyleSelector {
		#region overrides

		public override Style SelectStyle(object item, DependencyObject container) {
			Style style = new Style();
			style.TargetType = typeof(ListViewItem);
			Setter foregroundSetter = new Setter();
			foregroundSetter.Property = ListViewItem.ForegroundProperty;
			foregroundSetter.Value = GetForeground(item as LogAdapter);
			style.Setters.Add(foregroundSetter);

			return style;
		}

		#endregion


		#region privates

		private Brush GetForeground(LogAdapter log) {
			if (log != null) {
				switch (log.EventType) {
					case TraceEventType.Critical:
						return Brushes.Red;
					case TraceEventType.Error:
						return Brushes.Magenta;
					case TraceEventType.Warning:
						return Brushes.Olive;
					case TraceEventType.Information:
						return Brushes.Green;
				}
			}

			return Brushes.Gray;
		}

		#endregion
	}
}
