using System.Threading.Tasks;

namespace SilentNotes.Services;

public interface INotificationService
{
	Task ShowNextNotification();
}
