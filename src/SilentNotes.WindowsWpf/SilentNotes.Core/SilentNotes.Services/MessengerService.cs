using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;

namespace SilentNotes.Services;

public class MessengerService : IMessengerService
{
	[StructLayout(LayoutKind.Sequential, Size = 1)]
	private readonly struct MessengerToken : IEquatable<MessengerToken>
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(MessengerToken other)
		{
			return true;
		}

		public override bool Equals(object obj)
		{
			return obj is MessengerToken;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			return 0;
		}
	}

	public void Register<TMessage>(object recipient, MessageHandler<object, TMessage> handler) where TMessage : class
	{
		WeakReferenceMessenger.Default.Register<object, TMessage, MessengerToken>(recipient, default(MessengerToken), handler);
	}

	public void Unregister<TMessage>(object recipient) where TMessage : class
	{
		WeakReferenceMessenger.Default.Unregister<TMessage, MessengerToken>(recipient, default(MessengerToken));
	}

	public TMessage Send<TMessage>() where TMessage : class, new()
	{
		return WeakReferenceMessenger.Default.Send<TMessage, MessengerToken>(new TMessage(), default(MessengerToken));
	}

	public TMessage Send<TMessage>(TMessage message) where TMessage : class
	{
		return WeakReferenceMessenger.Default.Send<TMessage, MessengerToken>(message, default(MessengerToken));
	}
}
