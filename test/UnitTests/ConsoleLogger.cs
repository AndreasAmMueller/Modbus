using System;
using Microsoft.Extensions.Logging;

namespace UnitTests
{
	internal class ConsoleLogger : ILogger
	{
		private readonly object syncObj = new object();

		public ConsoleLogger()
		{
		}

		public ConsoleLogger(string name)
		{
			Name = name;
		}

		public ConsoleLogger(string name, ConsoleLogger parentLogger)
		{
			Name = name;
			ParentLogger = parentLogger;
		}

		public string Name { get; }

		public ConsoleLogger ParentLogger { get; }

		public bool DisableColors { get; set; }

		public string TimestampFormat { get; set; }

		public LogLevel MinLevel { get; set; }

		public IDisposable BeginScope<TState>(TState state)
		{
			throw new NotImplementedException();
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= MinLevel;
		}

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!IsEnabled(logLevel))
				return;

			if (formatter == null)
				throw new ArgumentNullException(nameof(formatter));

			string message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null)
			{
				if (ParentLogger != null)
				{
					ParentLogger.WriteMessage(Name, logLevel, eventId.Id, message, exception);
				}
				else
				{
					WriteMessage(Name, logLevel, eventId.Id, message, exception);
				}
			}
		}

		private void WriteMessage(string name, LogLevel logLevel, int eventId, string message, Exception exception)
		{
			if (exception != null)
			{
				if (!string.IsNullOrEmpty(message))
				{
					message += Environment.NewLine + exception.ToString();
				}
				else
				{
					message = exception.ToString();
				}
			}

			bool changedColor;
			string timestampPadding = "";
			lock (syncObj)
			{
				if (!string.IsNullOrEmpty(TimestampFormat))
				{
					changedColor = false;
					if (!DisableColors)
					{
						switch (logLevel)
						{
							case LogLevel.Trace:
								Console.ForegroundColor = ConsoleColor.DarkGray;
								changedColor = true;
								break;
						}
					}
					string timestamp = DateTime.Now.ToString(TimestampFormat) + " ";
					Console.Write(timestamp);
					timestampPadding = new string(' ', timestamp.Length);

					if (changedColor)
						Console.ResetColor();
				}

				changedColor = false;
				if (!DisableColors)
				{
					switch (logLevel)
					{
						case LogLevel.Trace:
							Console.ForegroundColor = ConsoleColor.DarkGray;
							changedColor = true;
							break;
						case LogLevel.Information:
							Console.ForegroundColor = ConsoleColor.DarkGreen;
							changedColor = true;
							break;
						case LogLevel.Warning:
							Console.ForegroundColor = ConsoleColor.Yellow;
							changedColor = true;
							break;
						case LogLevel.Error:
							Console.ForegroundColor = ConsoleColor.Black;
							Console.BackgroundColor = ConsoleColor.Red;
							changedColor = true;
							break;
						case LogLevel.Critical:
							Console.ForegroundColor = ConsoleColor.White;
							Console.BackgroundColor = ConsoleColor.Red;
							changedColor = true;
							break;
					}
				}
				Console.Write(GetLogLevelString(logLevel));

				if (changedColor)
					Console.ResetColor();

				changedColor = false;
				if (!DisableColors)
				{
					switch (logLevel)
					{
						case LogLevel.Trace:
							Console.ForegroundColor = ConsoleColor.DarkGray;
							changedColor = true;
							break;
					}
				}
				Console.WriteLine(": " + (!string.IsNullOrEmpty(name) ? "[" + name + "] " : "") + message.Replace("\n", "\n      " + timestampPadding));

				if (changedColor)
					Console.ResetColor();
			}
		}

		private static string GetLogLevelString(LogLevel logLevel)
		{
			switch (logLevel)
			{
				case LogLevel.Trace:
					return "trce";
				case LogLevel.Debug:
					return "dbug";
				case LogLevel.Information:
					return "info";
				case LogLevel.Warning:
					return "warn";
				case LogLevel.Error:
					return "fail";
				case LogLevel.Critical:
					return "crit";
				default:
					throw new ArgumentOutOfRangeException(nameof(logLevel));
			}
		}
	}
}
