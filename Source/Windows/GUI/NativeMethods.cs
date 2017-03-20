using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MAPE.Utils;


namespace MAPE.Windows.GUI {
	internal static class NativeMethods {
		#region types

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct RECT {
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;

			public RECT(int left, int top, int right, int bottom) {
				this.Left = left;
				this.Top = top;
				this.Right = right;
				this.Bottom = bottom;
			}

			public static bool operator ==(RECT x, RECT y) {
				return x.Left == y.Left && x.Top == y.Top && x.Right == y.Right && x.Bottom == y.Bottom;
			}

			public static bool operator !=(RECT x, RECT y) {
				return !(x == y);
			}

			public override bool Equals(object obj) {
				return (obj is RECT) ? (this == (RECT)obj) : false;
			}

			public override int GetHashCode() {
				return this.Left ^ this.Top ^ this.Right ^ this.Bottom;
			}
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct POINT {
			public int X;
			public int Y;

			public POINT(int x, int y) {
				this.X = x;
				this.Y = y;
			}

			public static bool operator ==(POINT x, POINT y) {
				return x.X == y.X && x.Y == y.Y;
			}

			public static bool operator !=(POINT x, POINT y) {
				return !(x == y);
			}

			public override bool Equals(object obj) {
				return (obj is POINT) ? (this == (POINT)obj) : false;
			}

			public override int GetHashCode() {
				return this.X ^ this.Y;
			}
		}

		[Serializable]
		[StructLayout(LayoutKind.Sequential)]
		public struct WINDOWPLACEMENT {
			public int Length;
			public int Flags;
			public int ShowCmd;
			public POINT MinPosition;
			public POINT MaxPosition;
			public RECT NormalPosition;


			public static bool operator ==(WINDOWPLACEMENT x, WINDOWPLACEMENT y) {
				// Length is ignored
				return (
					x.Flags == y.Flags &&
					x.ShowCmd == y.ShowCmd &&
					x.MinPosition == y.MinPosition &&
					x.MaxPosition == y.MaxPosition &&
					x.NormalPosition == y.NormalPosition
				);
			}

			public static bool operator !=(WINDOWPLACEMENT x, WINDOWPLACEMENT y) {
				return !(x == y);
			}

			public override bool Equals(object obj) {
				return (obj is WINDOWPLACEMENT) ? (this == (WINDOWPLACEMENT)obj) : false;
			}

			public override int GetHashCode() {
				return this.Flags ^ this.ShowCmd ^ this.MinPosition.GetHashCode() ^ this.MaxPosition.GetHashCode() ^ this.NormalPosition.GetHashCode();
			}
		}

		#endregion


		#region constants

		public const int SW_SHOWNORMAL = 1;
		public const int SW_SHOWMINIMIZED = 2;

		#endregion


		#region methods

		[DllImport("user32.dll")]
		public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

		[DllImport("user32.dll")]
		public static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

		#endregion
	}
}
