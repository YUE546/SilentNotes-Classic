using CommunityToolkit.Mvvm.Messaging;

namespace SilentNotes.Services;

public interface IMessengerService
{
	void Register<TMessage>(object recipient, MessageHandler<object, TMessage> handler) where TMessage : class;

	void Unregister<TMessage>(object recipient) where TMessage : class;

	TMessage Send<TMessage>() where TMessage : class, new();

	TMessage Send<TMessage>(TMessage message) where TMessage : class;
}
