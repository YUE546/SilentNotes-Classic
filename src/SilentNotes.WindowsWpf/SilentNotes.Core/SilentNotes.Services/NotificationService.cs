using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SilentNotes.Models;

namespace SilentNotes.Services;

public class NotificationService : INotificationService
{
	private class Notification
	{
		public Guid Id { get; set; }

		public Func<string> GetMessage { get; set; }

		public TimeSpan QueueTime { get; set; }
	}

	public static readonly Guid TransferCodeNotificationId = new Guid("cf497f24-61a0-4ddd-8af8-76faa59c6eff");

	private readonly IFeedbackService _feedbackService;

	private readonly ILanguageService _languageService;

	private readonly ISettingsService _settingsService;

	private List<Notification> Notifications { get; }

	public NotificationService(IFeedbackService feedbackService, ILanguageService languageService, ISettingsService settingsService)
	{
		_feedbackService = feedbackService;
		_languageService = languageService;
		_settingsService = settingsService;
		Notifications = new List<Notification>
		{
			new Notification
			{
				Id = TransferCodeNotificationId,
				GetMessage = () => _languageService.LoadTextFmt("transfer_code_notification", _languageService.LoadText("show_transfer_code")),
				QueueTime = TimeSpan.FromDays(5.0)
			}
		};
	}

	public async Task ShowNextNotification()
	{
		SettingsModel settings = _settingsService.LoadSettingsOrDefault();
		DateTime now = DateTime.UtcNow;
		AutoAddNotifications(settings);
		foreach (Notification notification in Notifications)
		{
			NotificationTriggerModel trigger = settings.NotificationTriggers.Find((NotificationTriggerModel item) => item.Id == notification.Id);
			if (trigger?.IsDue(now, notification.QueueTime) ?? false)
			{
				trigger.ShownAt = now;
				_settingsService.TrySaveSettingsToLocalDevice(settings);
				await _feedbackService.ShowMessageAsync(notification.GetMessage(), string.Empty, MessageBoxButtons.Ok, conservativeDefault: true);
				break;
			}
		}
	}

	private void AutoAddNotifications(SettingsModel settings)
	{
		NotificationTriggerModel notificationTriggerModel = settings.NotificationTriggers.Find((NotificationTriggerModel notificationTriggerModel2) => notificationTriggerModel2.Id == TransferCodeNotificationId);
		if (notificationTriggerModel == null && settings.HasTransferCode)
		{
			NotificationTriggerModel item = new NotificationTriggerModel
			{
				Id = TransferCodeNotificationId
			};
			settings.NotificationTriggers.Add(item);
			_settingsService.TrySaveSettingsToLocalDevice(settings);
		}
	}
}
