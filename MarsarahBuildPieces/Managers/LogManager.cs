using System;
using UnityEngine;

namespace MarsarahBuildPieces.Managers
{
	internal class LogManager
	{
		public enum LogLevel
		{
			None = 0,
			Error = 1,
			Warning = 2,
			Info = 3
		}

		private readonly string classLogTitle;
		private LogLevel classLogLevel;
		private static LogLevel globalLogLevel = LogLevel.None;

		public LogManager(string logTitle, LogLevel level)
		{
			classLogTitle = logTitle;
			classLogLevel = level;
		}

		public static void SetGlobalLogLevel(LogLevel level)
		{
			globalLogLevel = level;
		}

		public void SetClassLogLevel(LogLevel level)
		{
			classLogLevel = level;
		}

		// Determine if a message at the given level should be logged
		private bool ShouldLog(LogLevel messageLevel)
		{
			// Only log if message level passes both class and global levels
			return messageLevel <= classLogLevel && messageLevel <= globalLogLevel;
		}

		public void Info(string message, bool header = false, bool footer = false)
		{
			if (!ShouldLog(LogLevel.Info)) return;
			if (header) Debug.Log("===================================================");
			Debug.Log($"[Marsarah Build Pieces] [{classLogTitle}] : {message}");
			if (footer) Debug.Log("===================================================");
		}

		public void Warn(string message, bool header = false, bool footer = false)
		{
			if (!ShouldLog(LogLevel.Warning)) return;
			if (header) Debug.Log("===================================================");
			Debug.LogWarning($"[Marsarah Build Pieces] [{classLogTitle}] : {message}");
			if (footer) Debug.Log("===================================================");
		}

		public void Error(string message, bool header = false, bool footer = false)
		{
			if (!ShouldLog(LogLevel.Error)) return;
			if (header) Debug.Log("===================================================");
			Debug.LogError($"[Marsarah Build Pieces] [{classLogTitle}] : {message}");
			if (footer) Debug.Log("===================================================");
		}
	}
}
