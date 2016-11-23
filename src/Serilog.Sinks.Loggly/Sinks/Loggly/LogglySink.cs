﻿// Copyright 2014 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loggly;
using Loggly.Config;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using SyslogLevel=Loggly.Transports.Syslog.Level;

namespace Serilog.Sinks.Loggly
{
    /// <summary>
    /// Writes log events to the Loggly.com service.
    /// </summary>
    public class LogglySink : PeriodicBatchingSink
    {
        readonly IFormatProvider _formatProvider;
        LogglyClient _client;

        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 10;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Construct a sink that saves logs to the specified storage account. Properties are being send as data and the level is used as tag.
        /// </summary>
        /// <param name="batchSizeLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        ///  <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        public LogglySink(IFormatProvider formatProvider, int batchSizeLimit, TimeSpan period)
            : base (batchSizeLimit, period)
        {
            _formatProvider = formatProvider;

            _client = new LogglyClient();
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>Override either <see cref="PeriodicBatchingSink.EmitBatch"/> or <see cref="PeriodicBatchingSink.EmitBatchAsync"/>,
        /// not both.</remarks>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            await _client.Log(events.Select(CreateLogglyEvent));
        }

        private LogglyEvent CreateLogglyEvent(LogEvent logEvent)
        {
            var logglyEvent = new LogglyEvent() { Timestamp = logEvent.Timestamp };

            var isHttpTransport = LogglyConfig.Instance.Transport.LogTransport == LogTransport.Https;
            logglyEvent.Syslog.Level = ToSyslogLevel(logEvent);

            logglyEvent.Data.AddIfAbsent("Message", logEvent.RenderMessage(_formatProvider));

            foreach (var key in logEvent.Properties.Keys)
            {
                var propertyValue = logEvent.Properties[key];
                var simpleValue = LogglyPropertyFormatter.Simplify(propertyValue);
                logglyEvent.Data.AddIfAbsent(key, simpleValue);
            }

            if (isHttpTransport)
            {
                // syslog will capture these via the header
                logglyEvent.Data.AddIfAbsent("Level", logEvent.Level.ToString());
            }

            if (logEvent.Exception != null)
            {
                logglyEvent.Data.AddIfAbsent("Exception", logEvent.Exception);
            }
            return logglyEvent;
        }

        static SyslogLevel ToSyslogLevel(LogEvent logEvent)
        {
            SyslogLevel syslogLevel;
            // map the level to a syslog level in case that transport is used.
            switch (logEvent.Level)
            {
                case LogEventLevel.Verbose:
                case LogEventLevel.Debug:
                    syslogLevel = SyslogLevel.Notice;
                    break;
                case LogEventLevel.Information:
                    syslogLevel = SyslogLevel.Information;
                    break;
                case LogEventLevel.Warning:
                    syslogLevel = SyslogLevel.Warning;
                    break;
                case LogEventLevel.Error:
                case LogEventLevel.Fatal:
                    syslogLevel = SyslogLevel.Error;
                    break;
                default:
                    SelfLog.WriteLine("Unexpected logging level, writing to loggly as Information");
                    syslogLevel = SyslogLevel.Information;
                    break;
            }
            return syslogLevel;
        }
    }
}
