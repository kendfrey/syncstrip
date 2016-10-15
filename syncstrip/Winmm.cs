using System;
using System.Runtime.InteropServices;
using System.Text;

namespace syncstrip
{
	class Winmm
	{
		[DllImport("winmm.dll")]
		static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

		public static string mciSendString(string command)
		{
			StringBuilder returnString = new StringBuilder(256);
			mciSendString(command, returnString, 256, IntPtr.Zero);
			return returnString.ToString();
		}
	}
}
