namespace SilentNotes;

public class StoreUnsavedDataMessage
{
	public MessageSender Sender { get; }

	internal StoreUnsavedDataMessage()
		: this(MessageSender.Unknown)
	{
	}

	public StoreUnsavedDataMessage(MessageSender sender)
	{
		Sender = sender;
	}
}
