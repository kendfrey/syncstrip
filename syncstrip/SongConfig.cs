using Newtonsoft.Json;

namespace syncstrip
{
	class SongConfig
	{
		[JsonProperty("audioFile")]
		public string AudioFile
		{
			get;
			set;
		}

		[JsonProperty("bitmapFile")]
		public string BitmapFile
		{
			get;
			set;
		}

		[JsonProperty("millisecondsPerFrame")]
		public int MillisecondsPerFrame
		{
			get;
			set;
		}
	}
}
