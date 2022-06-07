﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Core.Services
{
    public class LogService : ILogService, ISingletonService
    {
        private readonly IServiceProvider serviceProvider;

        private bool updatedLogTable;

        public LogService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task LogDebug<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            await Log(logger, LogLevel.Debug, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogInformation<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            await Log(logger, LogLevel.Information, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogWarning<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            await Log(logger, LogLevel.Warning, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogError<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            await Log(logger, LogLevel.Error, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogCritical<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            await Log(logger, LogLevel.Critical, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task Log<T>(ILogger<T> logger, LogLevel logLevel, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = 0, int order = 0)
        {
            if (logLevel < logSettings.LogMinimumLevel)
            {
                return;
            }

            switch (logScope)
            {
                // Log the message if the scope is allowed to log or if log is at least a warning.
                case LogScopes.StartAndStop when logSettings.LogStartAndStop || logLevel > LogLevel.Information:
                case LogScopes.RunStartAndStop when logSettings.LogRunStartAndStop || logLevel > LogLevel.Information:
                case LogScopes.RunBody when logSettings.LogRunBody || logLevel > LogLevel.Information:
                {
                    using var scope = serviceProvider.CreateScope();
                    using var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();

                    // Update log table if it has not already been done since launch. The table definitions can only change when the AIS restarts with a new update.
                    if (!updatedLogTable)
                    {
                        var databaseHelpersService = scope.ServiceProvider.GetRequiredService<IDatabaseHelpersService>();
                        await databaseHelpersService.CheckAndUpdateTablesAsync(new List<string> {WiserTableNames.AisLogs});
                        updatedLogTable = true;
                    }
                    
                    logger.Log(logLevel, message);

                    databaseConnection.ClearParameters();
                    databaseConnection.AddParameter("message", message);
                    databaseConnection.AddParameter("level", logLevel.ToString());
                    databaseConnection.AddParameter("scope", logScope.ToString());
                    databaseConnection.AddParameter("source", typeof(T).Name);
                    databaseConnection.AddParameter("configuration", configurationName);
                    databaseConnection.AddParameter("timeId", timeId);
                    databaseConnection.AddParameter("order", order);
                    databaseConnection.AddParameter("addedOn", DateTime.Now);
                    await databaseConnection.ExecuteAsync(@$"INSERT INTO {WiserTableNames.AisLogs} (message, level, scope, source, configuration, time_id, `order`, added_on)
                                                                    VALUES(?message, ?level, ?scope, ?source, ?configuration, ?timeId, ?order, ?addedOn)");
                    break;
                }

                // Stop when the scope is evaluated above but is not allowed to log, to prevent the default exception to be thrown.
                case LogScopes.StartAndStop:
                case LogScopes.RunStartAndStop:
                case LogScopes.RunBody:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(logScope), logScope.ToString());
            }
        }
    }
}