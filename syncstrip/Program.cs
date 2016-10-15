using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace syncstrip
{
	static class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 1 || args.Length > 2)
			{
				ShowHelp();
				return;
			}
			string songFile = args[0];
			string songDir = Path.GetDirectoryName(songFile);
			if (!File.Exists(songFile))
			{
				ShowHelp();
				return;
			}
			int startTime = 0;
			if (args.Length == 2 && !int.TryParse(args[1], out startTime))
			{
				ShowHelp();
				return;
			}

			ProgramConfig config = JsonConvert.DeserializeObject<ProgramConfig>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json")));
			Console.WriteLine("Loaded program settings");
			SongConfig song = JsonConvert.DeserializeObject<SongConfig>(File.ReadAllText(songFile));
			Console.WriteLine($"Loaded song settings from {songFile}");

			// Initialize audio
			Winmm.mciSendString($"open \"{Path.Combine(songDir, song.AudioFile)}\" alias audio");
			Winmm.mciSendString($"set audio time format milliseconds");
			Winmm.mciSendString($"seek audio to {startTime}");

			// Initialize bitmap
			byte[] data;
			int pixels;
			int rows;
			int stride;
			using (Bitmap bitmap = new Bitmap(Path.Combine(songDir, song.BitmapFile)))
			{
				BitmapData bitmapData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
				pixels = bitmapData.Width;
				rows = bitmapData.Height;
				stride = bitmapData.Stride;
				data = new byte[stride * rows];
				Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
				bitmap.UnlockBits(bitmapData);
			}
			Console.WriteLine($"Loaded animation from {song.BitmapFile}");

			// Initialize serial device
			using (SerialPort serial = new SerialPort(config.SerialPort, 1228800))
			{
				serial.DtrEnable = true; // Triggers a board reset, which prevents some strange junk bytes arriving to the chip
				serial.RtsEnable = true;
				serial.Open();
				Console.WriteLine($"Opened serial port {config.SerialPort}");

				// Throw away any stale data
				serial.DiscardInBuffer();

				// Wait for the board to reset
				Console.WriteLine("Waiting for device to reset...");
				serial.ReadByte();
				Console.WriteLine("Device ready");

				// Initialize various playing data
				int frame = startTime / song.MillisecondsPerFrame;
				int nextFrameTime = -startTime % song.MillisecondsPerFrame;
				Stopwatch timer = new Stopwatch();
				byte[] rowHeader = new byte[] { 0x01, (byte)pixels };
				byte[] rowFooter = new byte[] { 0x00 };

				// Send first frame
				serial.Write(rowHeader, 0, rowHeader.Length);
				serial.Write(data, frame * stride, pixels * 3);
				Thread.Sleep(song.MillisecondsPerFrame);

				// Start audio
				Console.WriteLine($"Playing audio {song.AudioFile}...");
				Winmm.mciSendString("play audio");

				// Render first frame
				timer.Start();
				serial.Write(rowFooter, 0, rowFooter.Length);

				// Loop through remaining frames
				Console.WriteLine("Press any key to stop...");
				for (; frame < rows && !Console.KeyAvailable; frame++)
				{
					// Don't send until the board responds, meaning it's finished displaying the frame
					serial.ReadByte();

					// Write frame data
					serial.Write(rowHeader, 0, rowHeader.Length);
					serial.Write(data, frame * stride, pixels * 3);

					// Wait until the correct time
					nextFrameTime += song.MillisecondsPerFrame;
					timer.WaitUntil(nextFrameTime);

					// Tell the board to update the display
					serial.Write(rowFooter, 0, rowFooter.Length);
				}

				// Finish last frame
				nextFrameTime += song.MillisecondsPerFrame;
				timer.WaitUntil(nextFrameTime);

				// Indicate the end of the render
				serial.Write(new byte[] { 0xFF }, 0, 1);

				timer.Stop();
				Console.WriteLine("Animation finished");
				while (Console.KeyAvailable)
				{
					Console.ReadKey();
				}
			}
		}

		private static void WaitUntil(this Stopwatch timer, int time)
		{
			int delay = time - (int)timer.ElapsedMilliseconds;
			if (delay > 0)
			{
				Thread.Sleep(delay); // TODO: Maybe replace with a spin wait, if there are problems with sleeping too long
			}
		}

		private static void ShowHelp()
		{
			Console.WriteLine("Usage:   syncstrip <config> [<startTimeInMilliseconds>]");
			Console.WriteLine("Example: syncstrip songConfig.json 1000");
		}
	}
}
