using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JellyfinDiscordAuth.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth
{
    /// <summary>
    /// Task to update the Discord bot status with the active running task from the dashboard.
    /// </summary>
    public class TaskRunDiscordStatus : IScheduledTask
    {
        private readonly ITaskManager _taskManager;
        private readonly DiscordBotService _discordBotService;
        private readonly ILogger<TaskRunDiscordStatus> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskRunDiscordStatus"/> class.
        /// </summary>
        public TaskRunDiscordStatus(
            ILogger<TaskRunDiscordStatus> logger,
            ITaskManager taskManager,
            DiscordBotService discordBotService)
        {
            _logger = logger;
            _taskManager = taskManager;
            _discordBotService = discordBotService;

            _logger.LogInformation("TaskRunDiscordStatus Loaded");
        }

        /// <inheritdoc />
        public string Name => "Discord Bot Status";

        /// <inheritdoc />
        public string Key => "DiscordBotStatus";

        /// <inheritdoc />
        public string Description => "Updates the Discord bot status with the active running task from the dashboard.";

        /// <inheritdoc />
        public string Category => "Discord";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var trigger = new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                DayOfWeek = 0,
                IntervalTicks = 600000000, // 1 minute
            };
            return new[] { trigger };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var tasks = _taskManager.ScheduledTasks.Where(t => t.State == TaskState.Running && t.CurrentProgress.HasValue).ToList();
            if (!tasks.Any())
            {
                _logger.LogInformation("No tasks are currently running, clearing Discord bot status.");
                if (_discordBotService.Client != null)
                {
                    await _discordBotService.Client.SetCustomStatusAsync(null).ConfigureAwait(false);
                }
            }
            else
            {
                string status = string.Join(", ", tasks.Select(t => $"{t.Name} ({t.CurrentProgress:F1}%)"));

                _logger.LogInformation("Updating Discord bot status: {Status}", status);
                if (_discordBotService.Client != null)
                {
                    await _discordBotService.Client.SetCustomStatusAsync(status).ConfigureAwait(false);
                }
            }
        }
    }
}
