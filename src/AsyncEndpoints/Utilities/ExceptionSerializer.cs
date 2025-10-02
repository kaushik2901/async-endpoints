using System;
using System.Collections;
using System.Text;
using AsyncEndpoints.Configuration;

namespace AsyncEndpoints.Utilities
{
	/// <summary>
	/// Provides methods for serializing exceptions to strings while preserving all essential details.
	/// </summary>
	public static class ExceptionSerializer
	{
		/// <summary>
		/// Serializes an exception to a human-readable string with all essential details preserved.
		/// </summary>
		/// <param name="exception">The exception to serialize</param>
		/// <returns>A string representation of the exception with all essential details</returns>
		public static string Serialize(Exception exception)
		{
			if (exception == null)
				return string.Empty;

			var sb = new StringBuilder();
			SerializeException(sb, exception, 0);
			return sb.ToString();
		}

		private static void SerializeException(StringBuilder sb, Exception exception, int level)
		{
			var indent = new string(' ', level * 2); // 2-space indentation for each level

			sb.AppendLine($"{indent}Exception Type: {exception.GetType().FullName}");
			sb.AppendLine($"{indent}Message: {exception.Message}");
			sb.AppendLine($"{indent}Source: {exception.Source}");

			if (!string.IsNullOrEmpty(exception.StackTrace))
			{
				sb.AppendLine($"{indent}Stack Trace:");
				var stackTraceLines = exception.StackTrace.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
				foreach (var line in stackTraceLines)
				{
					sb.AppendLine($"{indent}  {line}");
				}
			}

			// Include HResult if available
			if (exception.HResult != 0)
			{
				sb.AppendLine($"{indent}HResult: {exception.HResult}");
			}

			// Include Inner Exception if present
			if (exception.InnerException != null)
			{
				sb.AppendLine($"{indent}Inner Exception:");
				SerializeException(sb, exception.InnerException, level + 1);
			}

			// Include Exception Data (custom key-value pairs)
			if (exception.Data?.Count > 0)
			{
				sb.AppendLine($"{indent}Data:");
				foreach (DictionaryEntry entry in exception.Data)
				{
					sb.AppendLine($"{indent}  {entry.Key}: {entry.Value}");
				}
			}

			if (level == 0) // Only add separator at the top level
			{
				sb.AppendLine(new string('-', AsyncEndpointsConstants.ExceptionSeparatorLength));
			}
		}
	}
}