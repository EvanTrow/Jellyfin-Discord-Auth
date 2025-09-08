using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace JellyfinDiscordAuth
{
    // Task to update the Discord bot status with the active running task from the dashboard.
    public class TaskRunDiscordStatus : IScheduledTask
    {
        private readonly ITaskManager _taskManager;
        private ILogger<TaskRunDiscordStatus> _logger;

        public TaskRunDiscordStatus(
            ILogger<TaskRunDiscordStatus> logger,
            ITaskManager taskManager)
        {
            _logger = logger;
            _taskManager = taskManager;

            _logger.LogInformation("TaskRunDiscordStatus Loaded");
        }

        public string Name => "Discord Bot Status";
        public string Key => "DiscordBotStatus";
        public string Description => "Updates the Discord bot status with the active running task from the dashboard.";
        public string Category => "Discord";

        // Defaults the triggers.
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            var trigger = new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                DayOfWeek = 0,
                IntervalTicks = 600000000, // 1 minute
            };
            return new[] { trigger };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var tasks = _taskManager.ScheduledTasks.Where(t => t.State == TaskState.Running && t.CurrentProgress.HasValue).ToList();
                if (!tasks.Any())
                {
                    _logger.LogInformation("No tasks are currently running, clearing Discord bot status.");
                    DiscordAuthPlugin.Client.SetCustomStatusAsync(null).Wait();
                }
                else
                {
                    string status = string.Join(", ", tasks.Select(t => $"{t.Name} ({t.CurrentProgress:F1}%)"));

                    _logger.LogInformation($"Updating Discord bot status: {status}");
                    DiscordAuthPlugin.Client.SetCustomStatusAsync(status).Wait();
                }
            }, cancellationToken);
        }
    }
}
