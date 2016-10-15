using Newtonsoft.Json;

namespace syncstrip
{
	class ProgramConfig
	{
		[JsonProperty("serialPort")]
		public string SerialPort
		{
			get;
			set;
		}
	}
}
